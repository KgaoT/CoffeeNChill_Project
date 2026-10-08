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
    public class QueueOrderFunction
    {
        private readonly IQueueService _queueService;
        private readonly ILogger<QueueOrderFunction> _logger;

        public QueueOrderFunction(IQueueService queueService, ILogger<QueueOrderFunction> logger)
        {
            _queueService = queueService;
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

            // OrderId and QueuedAt are server-generated: the client never
            // supplies its own order identity or timestamp.
            var order = new OrderMessage
            {
                OrderId = Guid.NewGuid().ToString(),
                CustomerName = request.CustomerName,
                QueuedAt = DateTimeOffset.UtcNow,
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
                _logger.LogError(ex, "Queue storage request failed while queuing order {OrderId}.", order.OrderId);

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
                queuedAt = order.QueuedAt,
                itemCount = order.Items.Count
            });
            return response;
        }

        private static async Task<HttpResponseData> BadRequest(HttpRequestData req, string error)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteAsJsonAsync(new { error });
            return response;
        }
    }
}
