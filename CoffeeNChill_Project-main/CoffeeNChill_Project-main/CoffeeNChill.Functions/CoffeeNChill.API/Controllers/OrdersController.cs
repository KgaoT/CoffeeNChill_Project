
using CoffeeNChill.API.DTOs.Orders;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.API.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrdersController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public OrdersController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // POST: api/orders/queue
        [HttpPost("queue")]
        public async Task<IActionResult> QueueOrder(
            [FromBody] CreateOrderRequest request)
        {
            var client = _httpClientFactory.CreateClient("Functions");

            var response = await client.PostAsJsonAsync(
                "api/orders/queue", request);

            var content = await response.Content.ReadAsStringAsync();

            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = content,
                ContentType = response.Content.Headers.ContentType?.ToString()
                    ?? "application/json"
            };
        }

        // PUT: api/orders/status
        [HttpPut("status")]
        public async Task<IActionResult> UpdateOrderStatus(
            [FromBody] UpdateOrderStatusRequest request)
        {
            var client = _httpClientFactory.CreateClient("Functions");

            var response = await client.PutAsJsonAsync(
                "api/orders/status", request);

            var content = await response.Content.ReadAsStringAsync();

            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = content,
                ContentType = response.Content.Headers.ContentType?.ToString()
                    ?? "application/json"
            };
        }
    }
}

