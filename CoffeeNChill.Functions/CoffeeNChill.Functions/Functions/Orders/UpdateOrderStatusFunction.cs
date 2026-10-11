using CoffeeNChill.Functions.DTOs;
using CoffeeNChill.Functions.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CoffeeNChill.Functions.Functions.Orders
{
    /// <summary>
    /// PUT /api/orders/status - forward-only status transition. Not yet
    /// authenticated/authorized: Part 3 of the module protects this route,
    /// but the brief asks for it to be built now so that work has something
    /// to secure.
    /// </summary>
    public class UpdateOrderStatusFunction
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<UpdateOrderStatusFunction> _logger;

        public UpdateOrderStatusFunction(IOrderService orderService, ILogger<UpdateOrderStatusFunction> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [Function("UpdateOrderStatus")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "orders/status")]
            HttpRequestData req)
        {
            UpdateOrderStatusRequest? request;
            try
            {
                request = await JsonSerializer.DeserializeAsync<UpdateOrderStatusRequest>(
                    req.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return await BadRequest(req, "The request body contains invalid JSON.");
            }

            if (request is null
                || string.IsNullOrWhiteSpace(request.OrderDate)
                || string.IsNullOrWhiteSpace(request.OrderId)
                || string.IsNullOrWhiteSpace(request.Status))
            {
                return await BadRequest(req, "orderDate, orderId and status are all required.");
            }

            _logger.LogInformation(
                "Attempting to move order {OrderId} ({OrderDate}) to status {Status}.",
                request.OrderId,
                request.OrderDate,
                request.Status);

            var result = await _orderService.UpdateStatusAsync(request.OrderDate, request.OrderId, request.Status);

            switch (result.Outcome)
            {
                case Interfaces.OrderStatusUpdateOutcome.NotFound:
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { error = result.Error });
                    return notFound;

                case Interfaces.OrderStatusUpdateOutcome.InvalidTransition:
                    return await BadRequest(req, result.Error ?? "Invalid status transition.");

                case Interfaces.OrderStatusUpdateOutcome.ConcurrencyConflict:
                    // Not one of the brief's two documented error cases for
                    // this endpoint (400 invalid/backward, 404 not found);
                    // 409 is the standard code for an ETag/optimistic-
                    // concurrency clash, which is what this actually is.
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteAsJsonAsync(new { error = result.Error });
                    return conflict;

                default:
                    _logger.LogInformation(
                        "Order {OrderId} moved to status {Status}.",
                        request.OrderId,
                        request.Status);

                    var ok = req.CreateResponse(HttpStatusCode.OK);
                    await ok.WriteAsJsonAsync(OrderResponse.FromEntity(result.Order!, includeHistory: true));
                    return ok;
            }
        }

        private static async Task<HttpResponseData> BadRequest(HttpRequestData req, string error)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteAsJsonAsync(new { error });
            return response;
        }
    }
}
