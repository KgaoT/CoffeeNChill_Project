# Part 2 demonstration video script

Target length: 5-8 minutes. Screen-record a terminal plus a browser tab for Azure Storage Explorer (or the Azurite table/queue views) and Postman. Order roughly follows the rubric so a marker can tick items off as they appear.

## 0. Before recording

- `docker compose down -v` and `docker image rm ghee9ine/coffeenchill-functions:v2.0 ghee9ine/coffeenchill-azurite:v2.0` so the pull in step 1 is real, not cached.
- Have this repo, Postman (with `CoffeeNChill-Part2.postman_collection.json`/environment imported), Azure Storage Explorer (or `az storage` CLI), and a terminal ready.

## 1. (0:00-1:00) `docker compose up -d` pulling `v2.0` from Docker Hub

- Run `docker compose up -d` in the repo root.
- Narrate while it runs: "Both images are pulled fresh from Docker Hub - `ghee9ine/coffeenchill-functions:v2.0` and `ghee9ine/coffeenchill-azurite:v2.0` - there's no local build involved."
- Point out the `Pulling` / `Pulled` lines in the output as visible proof.

## 2. (1:00-1:30) `docker ps`

- Run `docker ps`.
- Narrate: both containers running, `coffeenchill-azurite` showing `(healthy)`, `coffeenchill-functions` depends on that health check before it starts.

## 3. (1:30-4:00) Code walkthrough

Keep this brisk - point at the file, say one sentence about what it does, move on:

- `Functions/Orders/QueueOrderFunction.cs` - payload validation, the live `MenuItems` lookup that rejects unknown/unavailable SKUs by name, server-computed `TotalPrice`, the `ORD-{yyyy}-{5-digit}` id.
- `Services/QueueService.cs` - Base64 encoding on the queue client.
- `Functions/Orders/ProcessOrderQueueFunction.cs` - the dequeue log line, the idempotent `CreateAsync` call, the `Received -> Preparing -> Ready -> Collected` loop and its per-step delay.
- `Functions/Orders/ProcessPoisonOrdersFunction.cs` - what happens once `maxDequeueCount` is exceeded.
- `docker-compose.yml` - both services pulling from Docker Hub by default, the custom network, the named Azurite volume, the healthcheck.

## 4. (4:00-4:45) The `newman` run

- `cd docs && ./run-tests.sh` (or `.\run-tests.ps1`).
- Narrate while the two passes run: "This exercises the full menu CRUD, staff documents, the order queue producer's validation rules, the processing lifecycle, and every status-transition rule, end to end against the containers."
- Let it finish on screen: "All passes completed successfully."

## 5. (4:45-5:45) A message landing in the queue, live `Orders` table status changes

- In Postman, run **Orders: Queue / Queue a valid order** by itself.
- Switch to Azure Storage Explorer (or `az storage message peek --queue-name order-processing-queue ...`) and show the message - then show it gone a moment later once `ProcessOrderQueue` has dequeued it.
- Open the `Orders` table (or `GET /api/orders/{date}/{orderId}` repeatedly in Postman) and show `Status` advancing `Received -> Preparing -> Ready -> Collected` live, a few seconds apart.

## 6. (5:45-7:00) Poison-queue demo

- Run the command from the README's "Poison-queue demo" section to push a malformed message directly onto `order-processing-queue`.
- Show `docker logs coffeenchill-functions` scrolling through 5 retries (dequeue count 1 through 5).
- Show the message landing on `order-processing-queue-poison` (Storage Explorer or `az storage message peek`) and the corresponding row appearing in `FailedOrders`.

## 7. (7:00-7:30) Close

- `docker compose down` (optionally `-v`).
- One sentence recap: producer validation, queue-triggered processing with a full status lifecycle, poison-queue handling, Docker Compose pulling `v2.0` from Docker Hub, full Postman/newman coverage.
