using CoffeeNChill.Functions.DTOs;
using CoffeeNChill.Functions.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace CoffeeNChill.Functions.Functions
{
    public class UpdateOrderStatusFunction
    {
        private readonly IOrderTableService _orderTableService;
        private readonly ILogger<UpdateOrderStatusFunction> _logger;

        public UpdateOrderStatusFunction(
            IOrderTableService orderTableService,
            ILogger<UpdateOrderStatusFunction> logger)
        {
            _orderTableService = orderTableService;
            _logger = logger;
        }

        [Function("UpdateOrderStatus")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(
                AuthorizationLevel.Anonymous,
                "put",
                Route = "orders/status")]
            HttpRequestData req)
        {
            try
            {
                UpdateOrderStatusRequest? request;

                try
                {
                    request = await JsonSerializer.DeserializeAsync
                        <UpdateOrderStatusRequest>(
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

                if (request == null ||
                    string.IsNullOrWhiteSpace(request.OrderId) ||
                    string.IsNullOrWhiteSpace(request.Status) ||
                    request.OrderTimestamp == default)
                {
                    return await CreateErrorResponse(
                        req,
                        HttpStatusCode.BadRequest,
                        "OrderId, OrderTimestamp and Status are required.");
                }

                string partitionKey =
                    request.OrderTimestamp.UtcDateTime
                        .ToString("yyyy-MM-dd");

                await _orderTableService.UpdateOrderStatusAsync(
                    partitionKey,
                    request.OrderId.Trim(),
                    request.Status.Trim());

                _logger.LogInformation(
                    "Order {OrderId} status updated to {Status}.",
                    request.OrderId,
                    request.Status);

                HttpResponseData response =
                    req.CreateResponse(HttpStatusCode.OK);

                await response.WriteAsJsonAsync(new
                {
                    message = "Order status updated successfully.",
                    orderId = request.OrderId,
                    status = request.Status
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unable to update order status.");

                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.InternalServerError,
                    "Unable to update order status.");
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
