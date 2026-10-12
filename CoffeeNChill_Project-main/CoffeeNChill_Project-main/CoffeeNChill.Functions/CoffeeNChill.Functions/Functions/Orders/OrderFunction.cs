using CoffeeNChill.Functions.DTOs;
using CoffeeNChill.Functions.Models;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace CoffeeNChill.Functions.Functions
{
    public class OrderFunctions
    {
        private readonly IOrderQueueService _orderQueueService;
        private readonly ILogger<OrderFunctions> _logger;

        public OrderFunctions(
            IOrderQueueService orderQueueService,
            ILogger<OrderFunctions> logger)
        {
            _orderQueueService = orderQueueService;
            _logger = logger;
        }

        [Function("QueueOrder")]
        public async Task<HttpResponseData> QueueOrder(
            [HttpTrigger(
                AuthorizationLevel.Anonymous,
                "post",
                Route = "orders/queue")]
            HttpRequestData req)
        {
            try
            {
                CreateOrderRequest? request;

                try
                {
                    request = await JsonSerializer.DeserializeAsync<CreateOrderRequest>(
                        req.Body,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                }
                catch (JsonException)
                {
                    return await CreateErrorResponse(
                        req,
                        HttpStatusCode.BadRequest,
                        "Invalid JSON request body.");
                }

                if (request == null)
                {
                    return await CreateErrorResponse(
                        req,
                        HttpStatusCode.BadRequest,
                        "Request body is required.");
                }

                if (string.IsNullOrWhiteSpace(request.OrderId))
                {
                    return await CreateErrorResponse(
                        req,
                        HttpStatusCode.BadRequest,
                        "OrderId is required.");
                }

                if (string.IsNullOrWhiteSpace(request.CustomerName))
                {
                    return await CreateErrorResponse(
                        req,
                        HttpStatusCode.BadRequest,
                        "CustomerName is required.");
                }

                if (request.SelectedItemSKUs == null ||
                    request.SelectedItemSKUs.Count == 0 ||
                    request.SelectedItemSKUs.Any(
                        sku => string.IsNullOrWhiteSpace(sku)))
                {
                    return await CreateErrorResponse(
                        req,
                        HttpStatusCode.BadRequest,
                        "At least one valid SelectedItemSKU is required.");
                }

                if (request.TotalPrice <= 0)
                {
                    return await CreateErrorResponse(
                        req,
                        HttpStatusCode.BadRequest,
                        "TotalPrice must be greater than zero.");
                }

                var order = new Order
                {
                    OrderId = request.OrderId.Trim(),
                    CustomerName = request.CustomerName.Trim(),

                    SelectedItemSKUs = request.SelectedItemSKUs
                        .Select(sku => sku.Trim())
                        .ToList(),

                    TotalPrice = request.TotalPrice,

                    OrderTimestamp = DateTimeOffset.UtcNow
                };

                await _orderQueueService.SendOrderAsync(order);

                _logger.LogInformation(
                    "Order {OrderId} accepted for asynchronous processing.",
                    order.OrderId);

                HttpResponseData response =
                    req.CreateResponse(HttpStatusCode.Accepted);

                await response.WriteAsJsonAsync(new
                {
                    message = "Order queued successfully.",
                    orderId = order.OrderId,
                    status = "Queued",
                    orderTimestamp = order.OrderTimestamp
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An unexpected error occurred while queuing an order.");

                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.InternalServerError,
                    "Unable to queue the order at this time.");
            }
        }

        private static async Task<HttpResponseData> CreateErrorResponse(
            HttpRequestData req,
            HttpStatusCode statusCode,
            string errorMessage)
        {
            HttpResponseData response =
                req.CreateResponse(statusCode);

            await response.WriteAsJsonAsync(new
            {
                error = errorMessage
            });

            return response;
        }
    }
}
