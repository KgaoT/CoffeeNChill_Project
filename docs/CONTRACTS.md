# Order queue contracts

This describes what `POST /api/orders/queue` guarantees to whoever reads the queue next. Phase 1 (`fix/producer-brief-alignment`) aligned this to the Part 2 brief's exact message shape; Phase 2 implements the consumer, the Orders table, and the status lifecycle on top of it.

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

## What this branch does not do (left for Phase 2 / Phase 3 / Phase 4)

- No `ProcessOrderQueue` trigger, no writes to the `Orders` table, no status lifecycle (`Received` -> `Preparing` -> `Ready` -> `Collected`), no poison-queue handling, no `PUT /api/orders/status` endpoint.
- No `docker-compose`, no `v2.0` Docker Hub publish, no final combined Postman collection, no README changelog entry beyond this branch's own section, no video.
