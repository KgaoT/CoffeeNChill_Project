using CoffeeNChill.Functions.DTOs;
using CoffeeNChill.Functions.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace CoffeeNChill.Functions.Functions.Orders
{
    /// <summary>GET /api/orders, optionally filtered by ?date=yyyy-MM-dd.</summary>
    public class GetOrdersFunction
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<GetOrdersFunction> _logger;

        public GetOrdersFunction(IOrderService orderService, ILogger<GetOrdersFunction> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [Function("GetOrders")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")]
            HttpRequestData req)
        {
            var query = QueryHelpers.ParseQuery(req.Url.Query);
            string? date = query.TryGetValue("date", out var dateValues) ? dateValues.ToString() : null;

            _logger.LogInformation("Listing orders. Date filter: {Date}", date ?? "(none)");

            var orders = await _orderService.ListAsync(date);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(orders.Select(o => OrderResponse.FromEntity(o, includeHistory: false)));
            return response;
        }
    }
}
