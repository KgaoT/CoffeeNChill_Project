# Order queue contracts

This describes what `POST /api/orders/queue` (branch `feature/order-queue-producer`) guarantees to whoever reads the queue next. It covers the producer side only — nothing here implements the consumer, the Orders table, or the status lifecycle; those are Tetelo's and Palee's pieces.

## Storage names

Defined once in `Constants/StorageNames.cs` so every function/service refers to the same literal:

| Constant | Value | Used by |
|---|---|---|
| `StorageNames.OrdersQueue` | `orders-queue` | This branch (writer), Tetelo's `ProcessOrderQueue` trigger (reader) |
| `StorageNames.OrdersTable` | `Orders` | Not written by this branch — reserved for Tetelo's trigger |
| `StorageNames.MenuItemsTable` | `MenuItems` | Existing Part 1 table |
| `StorageNames.StaffDocsContainer` | `staff-docs` | Existing Part 1 Azurite Blob mode container |

## Queue message encoding

Messages are sent with `QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 }`. A `QueueTrigger` binding using the same `QueueMessageEncoding.Base64` option (or `[QueueTrigger("orders-queue")]` with the Base64 encoding configured on the worker, matching this project's convention) will receive the decoded JSON string directly — no manual Base64 handling needed on the consumer side.

## Message schema (`Models/OrderMessage.cs`)

```json
{
  "OrderId": "dc7c8d2e-6320-4d54-ab88-8eb2e44e797f",
  "CustomerName": "Thandi",
  "Items": [
    { "Category": "Hot Drinks", "SKU": "CAP-001", "Quantity": 2 },
    { "Category": "Pastries", "SKU": "CRO-003", "Quantity": 1 }
  ],
  "QueuedAt": "2026-10-08T18:57:32.7003249+00:00"
}
```

- `OrderId` — a GUID string, generated server-side by the producer. The client never supplies or influences this value; use it as the Orders table `RowKey` (or similar primary identifier).
- `CustomerName` — optional; may be `null` if the client omitted it.
- `Items` — always at least one entry. `Category`/`SKU` are validated non-blank and are expected to match an existing `MenuItems` entity's `PartitionKey`/`RowKey`, but this producer does **not** check menu-item existence against the table — that's left to the consumer if it needs to reject unknown SKUs.
- `Quantity` — always a positive integer (validated `> 0` before the message is queued).
- `QueuedAt` — server-side UTC timestamp (`DateTimeOffset`), set when the message was queued, not supplied by the client.

## What the producer already guarantees

- The payload has passed full validation before it ever reaches the queue: non-empty item list, non-blank category/SKU per item, positive quantity per item, non-blank customer name if one was supplied.
- `OrderId` and `QueuedAt` are always present and always server-generated — safe to treat as authoritative.
- A successful `POST` returns `202 Accepted` with `{ orderId, queuedAt, itemCount }` once the message is confirmed queued (not before).
- If the queue storage call itself fails (`RequestFailedException`), the client gets `502 Bad Gateway` and **nothing is queued** — there's no partial/ambiguous state to handle on the consumer side.

## What this branch does not do (left for Tetelo / Palee)

- No `ProcessOrderQueue` trigger, no writes to the `Orders` table, no status lifecycle (e.g. `Queued` → `Preparing` → `Ready`), no poison-queue handling, no `PUT /api/orders/status` endpoint.
- No `docker-compose`, no `v2.0` Docker Hub publish, no final combined Postman collection, no README changelog entry beyond this branch's own section, no video.
