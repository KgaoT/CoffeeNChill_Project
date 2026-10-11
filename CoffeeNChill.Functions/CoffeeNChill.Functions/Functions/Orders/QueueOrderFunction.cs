using Azure;
using CoffeeNChill.Functions.DTOs;
using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CoffeeNChill.Functions.Functions.Orders
{
    /// <summary>
    /// POST /api/orders/queue - validates an order against the live MenuItems
    /// table, computes its price server-side, and pushes it onto
    /// order-processing-queue for ProcessOrderQueue to pick up.
    /// </summary>
    public class QueueOrderFunction
    {
        // 5 digits (0-99999) keeps the collision space at 100,000 values per
        // calendar year. There is no defensive retry-on-collision loop here
        // because this producer has no Orders table dependency (that belongs
        // to Phase 2's consumer) - checking for a collision would mean
        // querying a table this function has no other reason to read. For a
        // student-project demo's order volume, a 1-in-100,000 chance per
        // order is an acceptable, documented trade-off; a production system
        // would either check the Orders table before enqueueing or use a
        // strictly monotonic/GUID-backed identifier instead.
        private const int OrderIdSuffixUpperBound = 100_000;

        private static readonly Random _random = new();

        private readonly IQueueService _queueService;
        private readonly ITableStorageService _tableStorageService;
        private readonly ILogger<QueueOrderFunction> _logger;

        public QueueOrderFunction(
            IQueueService queueService,
            ITableStorageService tableStorageService,
            ILogger<QueueOrderFunction> logger)
        {
            _queueService = queueService;
            _tableStorageService = tableStorageService;
            _logger = logger;
        }

        [Function("QueueOrder")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders/queue")]
            HttpRequestData req)
        {
            _logger.LogInformation("Queuing a new order.");

            CreateOrderRequest? request;
            try
            {
                request = await JsonSerializer.DeserializeAsync<CreateOrderRequest>(
                    req.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return await BadRequest(req, "The request body contains invalid JSON.");
            }

            if (request == null)
            {
                return await BadRequest(req, "Invalid request body.");
            }

            if (request.CustomerName is not null && string.IsNullOrWhiteSpace(request.CustomerName))
            {
                return await BadRequest(req, "CustomerName cannot be blank when provided.");
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                return await BadRequest(req, "At least one order item is required.");
            }

            for (int i = 0; i < request.Items.Count; i++)
            {
                var item = request.Items[i];

                if (string.IsNullOrWhiteSpace(item.Category))
                {
                    return await BadRequest(req, $"Items[{i}].Category is required.");
                }

                if (string.IsNullOrWhiteSpace(item.SKU))
                {
                    return await BadRequest(req, $"Items[{i}].SKU is required.");
                }

                if (item.Quantity <= 0)
                {
                    return await BadRequest(req, $"Items[{i}].Quantity must be greater than zero.");
                }
            }

            // Look up every line against the live MenuItems table: this is
            // both how TotalPrice gets computed and how unknown/unavailable
            // SKUs get caught, instead of trusting whatever the client sent.
            var badSkus = new List<string>();
            var priceByLineIndex = new double[request.Items.Count];

            for (int i = 0; i < request.Items.Count; i++)
            {
                var item = request.Items[i];
                MenuItem? menuItem = await _tableStorageService.GetMenuItemAsync(item.Category, item.SKU);

                if (menuItem is null || !menuItem.IsAvailable)
                {
                    badSkus.Add(item.SKU);
                    continue;
                }

                priceByLineIndex[i] = menuItem.Price;
            }

            if (badSkus.Count > 0)
            {
                return await BadRequest(
                    req,
                    $"Unknown or unavailable SKUs: {string.Join(", ", badSkus.Distinct())}.");
            }

            double totalPrice = 0;
            var selectedItemSkus = new List<string>();

            for (int i = 0; i < request.Items.Count; i++)
            {
                var item = request.Items[i];
                totalPrice += priceByLineIndex[i] * item.Quantity;

                for (int unit = 0; unit < item.Quantity; unit++)
                {
                    selectedItemSkus.Add(item.SKU);
                }
            }

            totalPrice = Math.Round(totalPrice, 2, MidpointRounding.AwayFromZero);

            // OrderId and OrderTimestamp are server-generated: the client
            // never supplies its own order identity, timestamp or total.
            var order = new OrderMessage
            {
                OrderId = GenerateOrderId(),
                CustomerName = request.CustomerName,
                SelectedItemSKUs = selectedItemSkus,
                TotalPrice = totalPrice,
                OrderTimestamp = DateTimeOffset.UtcNow,
                Items = request.Items.Select(i => new OrderMessageItem
                {
                    Category = i.Category,
                    SKU = i.SKU,
                    Quantity = i.Quantity
                }).ToList()
            };

            try
            {
                await _queueService.EnqueueOrderAsync(order);
            }
            catch (RequestFailedException ex)
            {
                // Logged without the exception's request/response detail,
                // which can carry the storage connection string in its
                // message for some failure modes - only the order id and
                // status code are safe to record.
                _logger.LogError(
                    "Queue storage request failed while queuing order {OrderId}. Status: {Status}",
                    order.OrderId,
                    ex.Status);

                // 502: the queue is an upstream dependency this function
                // relies on, and the request to it is what failed - not this
                // function's own availability (which would be 503).
                var failure = req.CreateResponse(HttpStatusCode.BadGateway);
                await failure.WriteAsJsonAsync(new
                {
                    error = "Could not queue the order because the storage service rejected the request."
                });
                return failure;
            }

            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new
            {
                orderId = order.OrderId,
                orderTimestamp = order.OrderTimestamp,
                totalPrice = order.TotalPrice,
                itemCount = order.Items.Count
            });
            return response;
        }

        private static string GenerateOrderId()
        {
            int suffix = _random.Next(0, OrderIdSuffixUpperBound);
            return $"ORD-{DateTime.UtcNow:yyyy}-{suffix:D5}";
        }

        private static async Task<HttpResponseData> BadRequest(HttpRequestData req, string error)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteAsJsonAsync(new { error });
            return response;
        }
    }
}
