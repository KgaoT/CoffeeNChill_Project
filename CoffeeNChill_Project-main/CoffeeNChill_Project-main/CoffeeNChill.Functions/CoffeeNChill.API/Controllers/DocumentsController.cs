
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace CoffeeNChill.API.Controllers
{
    [ApiController]
    [Route("api/documents")]
    public class DocumentsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public DocumentsController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // POST: api/documents
        // Upload a staff document
        [HttpPost]
        public async Task<IActionResult> UploadDocument(
            IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    error = "Please select a valid document."
                });
            }

            using var form = new MultipartFormDataContent();

            using var fileContent = new StreamContent(file.OpenReadStream());

            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue(
                    file.ContentType ?? "application/octet-stream");

            form.Add(fileContent, "file", file.FileName);

            var client = _httpClientFactory.CreateClient("Functions");

            var response = await client.PostAsync(
                "api/documents/upload", form);

            var content = await response.Content.ReadAsStringAsync();

            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = content,
                ContentType = response.Content.Headers.ContentType?.ToString()
                    ?? "application/json"
            };
        }

        // GET: api/documents
        // Retrieve all staff documents
        [HttpGet]
        public async Task<IActionResult> GetAllDocuments()
        {
            var client = _httpClientFactory.CreateClient("Functions");

            var response = await client.GetAsync("api/documents");

            var content = await response.Content.ReadAsStringAsync();

            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = content,
                ContentType = response.Content.Headers.ContentType?.ToString()
                    ?? "application/json"
            };
        }

        // DELETE: api/documents/{fileName}
        // Delete a staff document
        [HttpDelete("{fileName}")]
        public async Task<IActionResult> DeleteDocument(
            string fileName)
        {
            var client = _httpClientFactory.CreateClient("Functions");

            var response = await client.DeleteAsync(
                $"api/documents/{Uri.EscapeDataString(fileName)}");

            return StatusCode((int)response.StatusCode);
        }

        // GET: api/documents/download/{fileName}
        // Download a staff document
        [HttpGet("download/{fileName}")]
        public async Task<IActionResult> DownloadDocument(
            string fileName)
        {
            var client = _httpClientFactory.CreateClient("Functions");

            var response = await client.GetAsync(
                $"api/documents/download/{Uri.EscapeDataString(fileName)}");

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode);
            }

            var fileBytes = await response.Content.ReadAsByteArrayAsync();

            var contentType = response.Content.Headers.ContentType?.ToString()
                ?? "application/octet-stream";

            return File(fileBytes, contentType, fileName);
        }
    }
}

