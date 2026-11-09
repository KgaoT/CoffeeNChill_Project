using CoffeeNChill.API.DTOs.Menu;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.API.Controllers
{
    [ApiController]
    [Route("api/menu")]
    public class MenuController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public MenuController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // GET: api/menu
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

        // POST: api/menu
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

        // PUT: api/menu/{category}/{sku}
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

        // DELETE: api/menu/{category}/{sku}
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
