using CoffeeNChill.Functions.DTOs;
using CoffeeNChill.Functions.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace CoffeeNChill.Functions.Functions.Orders
{
    /// <summary>GET /api/orders/{date}/{orderId} - includes StatusHistory.</summary>
    public class GetOrderFunction
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<GetOrderFunction> _logger;

        public GetOrderFunction(IOrderService orderService, ILogger<GetOrderFunction> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [Function("GetOrder")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{date}/{orderId}")]
            HttpRequestData req,
            string date,
            string orderId)
        {
            _logger.LogInformation("Looking up order {OrderId} for date {Date}.", orderId, date);

            var order = await _orderService.GetAsync(date, orderId);

            if (order is null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(new { error = $"No order '{orderId}' found for date '{date}'." });
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(OrderResponse.FromEntity(order, includeHistory: true));
            return response;
        }
    }
}
