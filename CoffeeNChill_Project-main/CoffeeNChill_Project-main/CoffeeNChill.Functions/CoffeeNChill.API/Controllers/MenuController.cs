using CoffeeNChill.API.DTOs.Menu;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.API.Controllers
{
    /// <summary>
    /// Provides API endpoints for managing CoffeeNChill menu items.
    /// </summary>
    [ApiController]
    [Route("api/menu")]
    public class MenuController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public MenuController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Retrieves all available menu items.
        /// </summary>
        /// <returns>A list of menu items.</returns>
        [HttpGet]
        public async Task<IActionResult> GetAllMenuItems()
        {
            var client = _httpClientFactory.CreateClient("Functions");

            var response = await client.GetAsync("api/menu");

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
        /// Creates a new menu item.
        /// </summary>
        /// <param name="request">The menu item details.</param>
        /// <returns>The response from the Azure Function.</returns>
        [HttpPost]
        public async Task<IActionResult> CreateMenuItem(
            [FromBody] CreateMenuItemRequest request)
        {
            var client = _httpClientFactory.CreateClient("Functions");

            var response = await client.PostAsJsonAsync(
                "api/menu", request);

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
        /// Updates an existing menu item.
        /// </summary>
        /// <param name="category">The menu item's category.</param>
        /// <param name="sku">The menu item's unique SKU.</param>
        /// <param name="request">The updated menu item details.</param>
        /// <returns>The response from the Azure Function.</returns>
        [HttpPut("{category}/{sku}")]
        public async Task<IActionResult> UpdateMenuItem(
            string category,
            string sku,
            [FromBody] UpdateMenuItemRequest request)
        {
            var client = _httpClientFactory.CreateClient("Functions");

            var response = await client.PutAsJsonAsync(
                $"api/menu/{Uri.EscapeDataString(category)}/{Uri.EscapeDataString(sku)}",
                request);

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
        /// Deletes an existing menu item.
        /// </summary>
        /// <param name="category">The menu item's category.</param>
        /// <param name="sku">The menu item's unique SKU.</param>
        /// <returns>The response from the Azure Function.</returns>
        [HttpDelete("{category}/{sku}")]
        public async Task<IActionResult> DeleteMenuItem(
            string category,
            string sku)
        {
            var client = _httpClientFactory.CreateClient("Functions");

            var response = await client.DeleteAsync(
                $"api/menu/{Uri.EscapeDataString(category)}/{Uri.EscapeDataString(sku)}");

            return StatusCode((int)response.StatusCode);
        }
    }
}

