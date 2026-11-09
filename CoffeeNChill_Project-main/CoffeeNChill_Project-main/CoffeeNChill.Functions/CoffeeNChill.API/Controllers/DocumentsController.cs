using Microsoft.AspNetCore.Authorization;
using CoffeeNChill.API.DTOs.Documents;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.API.Controllers
{
    /// <summary>
    /// Provides API endpoints for managing staff documents.
    /// </summary>
    [Authorize(Policy = "StaffOrAdmin")]
    [ApiController]
    [Route("api/documents")]
    public class DocumentsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public DocumentsController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Uploads a staff document.
        /// </summary>
        /// <param name="file">The document to upload.</param>
        /// <returns>The upload result from the Azure Function.</returns>
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadDocument(
            IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Please select a valid file.");
            }

            var client = _httpClientFactory.CreateClient("Functions");

            using var form = new MultipartFormDataContent();

            using var fileContent = new StreamContent(file.OpenReadStream());

            fileContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(
                    file.ContentType ?? "application/octet-stream");

            form.Add(fileContent, "file", file.FileName);

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

        /// <summary>
        /// Retrieves all uploaded staff documents.
        /// </summary>
        /// <returns>A list of staff documents.</returns>
        [HttpGet]
        public async Task<IActionResult> GetDocuments()
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

        /// <summary>
        /// Downloads a staff document.
        /// </summary>
        /// <param name="fileName">The name of the document.</param>
        /// <returns>The requested document file.</returns>
        [HttpGet("download/{fileName}")]
        public async Task<IActionResult> DownloadDocument(string fileName)
        {
            var client = _httpClientFactory.CreateClient("Functions");

            var response = await client.GetAsync(
                $"api/documents/download/{Uri.EscapeDataString(fileName)}");

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode);
            }

            var fileBytes = await response.Content.ReadAsByteArrayAsync();

            var contentType = response.Content.Headers.ContentType?.MediaType
                ?? "application/octet-stream";

            return File(fileBytes, contentType, fileName);
        }

        /// <summary>
        /// Deletes an uploaded staff document.
        /// </summary>
        /// <param name="fileName">The name of the document to delete.</param>
        /// <returns>The response from the Azure Function.</returns>
        [HttpDelete("{fileName}")]
        public async Task<IActionResult> DeleteDocument(string fileName)
        {
            var client = _httpClientFactory.CreateClient("Functions");

            var response = await client.DeleteAsync(
                $"api/documents/{Uri.EscapeDataString(fileName)}");

            return StatusCode((int)response.StatusCode);
        }
    }
}


