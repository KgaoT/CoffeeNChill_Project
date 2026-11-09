using CoffeeNChill.API.DTOs.Orders;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.API.Controllers
{
    /// <summary>
    /// Provides API endpoints for managing customer orders.
    /// </summary>
    [ApiController]
    [Route("api/orders")]
    public class OrdersController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public OrdersController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Submits a customer order to the processing queue.
        /// </summary>
        /// <param name="request">The customer order details.</param>
        /// <returns>The response from the Azure Function.</returns>
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

        /// <summary>
        /// Updates the status of an existing customer order.
        /// </summary>
        /// <param name="request">The order ID, timestamp and new status.</param>
        /// <returns>The response from the Azure Function.</returns>
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

