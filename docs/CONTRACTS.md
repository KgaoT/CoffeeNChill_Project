# Order queue contracts

This describes the order queue end to end: what `POST /api/orders/queue` guarantees to whoever reads the queue next, and what `ProcessOrderQueue`/`ProcessPoisonOrders` do with it. Phase 1 (`fix/producer-brief-alignment`) aligned the producer to the Part 2 brief's exact message shape; Phase 2 (`feature/order-processing-function`) adds the consumer, the Orders/FailedOrders tables, and the status lifecycle on top of it.

## Storage names

Defined once in `Constants/StorageNames.cs` so every function/service refers to the same literal:

| Constant | Value | Used by |
|---|---|---|
| `StorageNames.OrdersQueue` | `order-processing-queue` | Producer (writer), `ProcessOrderQueue` trigger (reader) |
| `StorageNames.OrdersPoisonQueue` | `order-processing-queue-poison` | `ProcessPoisonOrders` trigger (reader). Created automatically by the Functions runtime once a message exceeds `host.json`'s `maxDequeueCount` - not created by any service in this codebase. |
| `StorageNames.OrdersTable` | `Orders` | `ProcessOrderQueue` trigger (writer) |
| `StorageNames.FailedOrdersTable` | `FailedOrders` | `ProcessPoisonOrders` trigger (writer) |
| `StorageNames.MenuItemsTable` | `MenuItems` | Existing Part 1 table; also read by the producer to validate SKUs and compute `TotalPrice` |
| `StorageNames.StaffDocsContainer` | `staff-docs` | Existing Part 1 Azurite Blob mode container |

## Queue message encoding

Messages are sent with `QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 }`, and `host.json` sets `extensions.queues.messageEncoding` to `"base64"` explicitly so a `[QueueTrigger]` binding decodes the same way without needing that configured per-function.

`host.json` also sets:

- `maxDequeueCount: 5` - a message is retried up to 5 times before the runtime moves it to `order-processing-queue-poison`.
- `visibilityTimeout: "00:00:10"` - 10 seconds. `ProcessOrderQueue`'s status-lifecycle simulation (Phase 2) must either finish within this window per dequeue or use a pattern that tolerates redelivery, since a message becomes re-visible (and will be picked up by another invocation) if processing runs longer than this.

## Message schema (`Models/OrderMessage.cs`)

```json
{
  "OrderId": "ORD-2026-08801",
  "CustomerName": "Thandi",
  "SelectedItemSKUs": ["CAP-001", "CAP-001", "CRO-003"],
  "TotalPrice": 95.00,
  "OrderTimestamp": "2026-10-11T18:57:32.7003249+00:00",
  "Items": [
    { "Category": "Hot Drinks", "SKU": "CAP-001", "Quantity": 2 },
    { "Category": "Pastries", "SKU": "CRO-003", "Quantity": 1 }
  ]
}
```

- `OrderId` - server-generated, format `ORD-{yyyy}-{5-digit zero-padded random number}` (e.g. `ORD-2026-08801`). The client never supplies or influences this value; use it as the Orders table `RowKey`.
  - **Collision-safety trade-off (documented per the brief's request):** the 5-digit suffix is a uniformly random value in `[0, 100000)`, not a retry-checked or strictly monotonic id. This producer has no reason to depend on the `Orders` table (that belongs to Phase 2's consumer), so adding a "query the table, retry if taken" loop here would introduce a dependency purely to protect against an outcome that's already a 1-in-100,000 chance per order - an acceptable trade-off at student-project order volumes. A production system would either check for a collision against the `Orders` table before enqueueing, or use a strictly unique identifier (e.g. a GUID or a database-generated sequence) instead.
- `CustomerName` - optional; may be `null` if the client omitted it.
- `SelectedItemSKUs` - a flat `string[]`, one entry per **unit** ordered (so a quantity of 2 for `CAP-001` appears twice), matching the brief's sample shape literally rather than only listing distinct SKUs.
- `TotalPrice` - computed server-side as `sum(MenuItems price x quantity)` across all lines, rounded to 2 decimal places. The client cannot influence this: `CreateOrderRequest` (the HTTP request DTO) has no `TotalPrice` field at all, so nothing resembling a client-supplied total is ever read, let alone trusted.
- `OrderTimestamp` - server-side UTC timestamp (`DateTimeOffset`, ISO 8601), set when the message was queued, not supplied by the client.
- `Items` - kept as an additional field beyond the brief's five, so the consumer has `Category`/`Quantity` per line without needing to reverse-engineer them from `SelectedItemSKUs`. Always at least one entry; `Category`/`SKU` are validated non-blank and must match an existing available `MenuItems` entity (see below).

## What the producer validates before anything is queued

1. **Payload shape**: non-empty item list, non-blank `Category`/`SKU` per item, positive `Quantity` per item, non-blank `CustomerName` if one was supplied, valid JSON.
2. **SKU existence and availability**: every `(Category, SKU)` pair is looked up against the live `MenuItems` table (`ITableStorageService.GetMenuItemAsync`). An item that doesn't exist, or exists but has `IsAvailable: false`, makes the whole request fail with `400 Bad Request` and a message naming every bad SKU, e.g. `"Unknown or unavailable SKUs: CAP-999."` Nothing is queued if any line fails this check.
3. Only after every line passes are `TotalPrice`, `OrderId` and `OrderTimestamp` computed and the message queued.

A successful `POST` returns `202 Accepted` with `{ orderId, orderTimestamp, totalPrice, itemCount }` once the message is confirmed queued (not before).

## Queue-connection error handling

If the queue storage call itself fails (`RequestFailedException`), the client gets **502 Bad Gateway** and nothing is queued - there's no partial/ambiguous state for the consumer to handle. 502 (not 503) because the failure is the producer's upstream dependency (queue storage) rejecting the request, not the producer itself being unavailable. The error is logged with the order id and the storage status code only - never the exception's full detail, which can carry the connection string for some Azure SDK failure modes.

## Orders table schema (`Models/OrderEntity.cs`)

Written by `ProcessOrderQueue`, read by `GetOrders`/`GetOrder`/`UpdateOrderStatus`.

| Column | Type | Notes |
|---|---|---|
| `PartitionKey` | string | The order's UTC date, `yyyy-MM-dd`, derived from `OrderTimestamp`. |
| `RowKey` | string | `OrderId`. |
| `CustomerName` | string? | |
| `SelectedItemSKUsJson` | string | JSON-serialized `string[]` - Table Storage has no native array type. |
| `TotalPrice` | double | |
| `OrderTimestamp` | DateTimeOffset | Business time the order was queued (not the storage-managed `Timestamp` column every `ITableEntity` already has). |
| `Status` | string | One of `Received`, `Preparing`, `Ready`, `Collected`. |
| `StatusUpdatedAt` | DateTimeOffset | |
| `StatusHistoryJson` | string | JSON-serialized `[{ "Status": "...", "At": "..." }, ...]`, oldest first. |

The HTTP endpoints (`GetOrders`, `GetOrder`) return this reshaped into camelCase `selectedItemSKUs` / `statusHistory` arrays (see `DTOs/OrderResponse.cs`), not the raw `*Json` string columns.

## FailedOrders table schema

Written by `ProcessPoisonOrders` for every message that exhausts `maxDequeueCount` retries.

| Column | Type | Notes |
|---|---|---|
| `PartitionKey` | string | UTC date (`yyyy-MM-dd`) the poison message was logged, **not** the order's own date - a malformed message may have no parseable `OrderTimestamp` at all. |
| `RowKey` | string | The queue message's `Id` (not the `OrderId` - a malformed message may have no parseable `OrderId` either). |
| `RawBody` | string | The exact message text that failed to process. |
| `Reason` | string | Static description (see below); the original exception lives in `ProcessOrderQueue`'s own logs around the same `InsertionTime`, not duplicated here. |
| `LoggedAt` | DateTimeOffset | |

## Order processing lifecycle (`ProcessOrderQueueFunction`)

1. Logs the queue message's `Id` and `DequeueCount`, then parses the body as `OrderMessage`.
2. **Malformed JSON, or a parsed message with a blank `OrderId`:** logged at `Error` with the raw body, then the exception is rethrown. The Functions runtime retries up to `host.json`'s `maxDequeueCount` (5), then moves the message to `order-processing-queue-poison` automatically - no code in this repo does that move itself.
3. Otherwise, writes an `Orders` row with `Status = "Received"` via `IOrderService.CreateAsync`.
   - **Idempotent on redelivery:** if that `OrderId` already has a row (an `AddEntity` `409`), `CreateAsync` fetches and returns the existing row instead of throwing. The function logs this and resumes the lifecycle from whatever status that row is already at, rather than restarting from `Received`.
4. Advances `Status` forward one step at a time (`Preparing` -> `Ready` -> `Collected`), waiting `OrderStatusStepDelaySeconds` (config, default `3`) before each step, persisting and logging every transition.
5. **Visibility-timeout trade-off (documented per the brief's request):** `host.json` fixes `visibilityTimeout` at 10 seconds (brief-mandated, Phase 1). At the default 3-second step delay, three remaining transitions take roughly 9 seconds of delay alone, before accounting for the table writes themselves - comfortably tight, not comfortably safe. If a dequeue ever runs past 10 seconds, the queue makes the message visible again and a second, concurrent invocation can start processing the same message. Two defenses make that safe rather than corrupting:
   - The idempotent create in step 3 means the second invocation never creates a duplicate `Received` row or duplicate history entry - it resumes instead.
   - Every status write goes through `IOrderService.UpdateStatusAsync`'s ETag optimistic concurrency. If the first invocation already advanced the order, the second invocation's write loses the ETag race and gets `OrderStatusUpdateOutcome.ConcurrencyConflict`; `ProcessOrderQueueFunction` logs this and simply stops its own loop rather than throwing, treating "another invocation already handled this" as success, not failure.
   - A production system would more likely increase `visibilityTimeout` well past the lifecycle's worst-case duration, or move the delayed stepping out of the trigger entirely (e.g. a durable orchestration or a self-scheduling queue message per step). Both were ruled out here only because the brief fixes `visibilityTimeout` at `00:00:10` explicitly.

## Status transition rules (`IOrderService.UpdateStatusAsync`, shared by the queue trigger and `PUT /api/orders/status`)

Canonical order: `Received` -> `Preparing` -> `Ready` -> `Collected`.

- **"Forward-only" is read as "strictly later in this list", not "exactly the next status".** A transition straight from `Received` to `Collected` is accepted. This is a deliberately looser reading than step-by-step-only, chosen because the brief requires rejecting backward moves but never requires rejecting a forward *jump*; see the next section's error-code mapping either way.
- Any status string not in the canonical list, or a target at or before the order's current status, is rejected before any write is attempted.
- A valid transition is written with `UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace)`; a `412` (ETag mismatch - something else wrote first) is reported as `ConcurrencyConflict`, not surfaced as a generic error.

## `PUT /api/orders/status`

Body: `{ "orderDate": "yyyy-MM-dd", "orderId": "...", "status": "Preparing" }`.

| Outcome | HTTP status |
|---|---|
| Valid forward transition | `200`, body is the updated order (including `StatusHistory`) |
| Order not found for that `orderDate`/`orderId` | `404` |
| Backward/same-status transition, or an unrecognized status string | `400` |
| ETag conflict (order was updated concurrently, e.g. by the queue trigger's own lifecycle advancing it at the same moment) | `409` - not one of the brief's two explicitly listed cases, added because it's a materially different situation from either of them and a generic error code would hide that from the caller |

Not yet authenticated or authorized - Part 3 of the module adds that; this route is deliberately buildable and testable now so there is something for Part 3 to secure.

## What Phase 2 does not do (left for Phase 3 / Phase 4)

- No `docker-compose`, no `v2.0` Docker Hub publish, no final combined Postman collection, no README changelog entry beyond each phase's own section, no video.
- `PUT /api/orders/status` has no authentication/authorization yet.
