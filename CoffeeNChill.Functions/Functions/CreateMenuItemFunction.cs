using CoffeeNChill.Functions.DTOs;
using CoffeeNChill.Functions.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CoffeeNChill.Functions.Functions
{
    public class CreateMenuItemFunction
    {
        private readonly ITableStorageService _tableStorageService;
        private readonly ILogger<CreateMenuItemFunction> _logger;

        public CreateMenuItemFunction(
            ITableStorageService tableStorageService,
            ILogger<CreateMenuItemFunction> logger)
        {
            _tableStorageService = tableStorageService;
            _logger = logger;
        }

        [Function("CreateMenuItem")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(
                AuthorizationLevel.Function,
                "post",
                Route = "menu")]
            HttpRequestData req)
        {
            _logger.LogInformation("Creating a new menu item.");

            try
            {
                // Read and deserialize request body
                var request =
                    await JsonSerializer.DeserializeAsync<CreateMenuItemRequest>(
                        req.Body,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                // Check whether the request body is valid
                if (request == null)
                {
                    var badRequest =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badRequest.WriteAsJsonAsync(new
                    {
                        error = "Invalid request body."
                    });

                    return badRequest;
                }

                // Validate Category
                if (string.IsNullOrWhiteSpace(request.Category))
                {
                    var badRequest =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badRequest.WriteAsJsonAsync(new
                    {
                        error = "Category is required."
                    });

                    return badRequest;
                }

                // Validate SKU
                if (string.IsNullOrWhiteSpace(request.SKU))
                {
                    var badRequest =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badRequest.WriteAsJsonAsync(new
                    {
                        error = "SKU is required."
                    });

                    return badRequest;
                }

                // Validate Name
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    var badRequest =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badRequest.WriteAsJsonAsync(new
                    {
                        error = "Name is required."
                    });

                    return badRequest;
                }

                // Validate Description
                if (string.IsNullOrWhiteSpace(request.Description))
                {
                    var badRequest =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badRequest.WriteAsJsonAsync(new
                    {
                        error = "Description is required."
                    });

                    return badRequest;
                }

                // Validate Price
                if (request.Price <= 0)
                {
                    var badRequest =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badRequest.WriteAsJsonAsync(new
                    {
                        error = "Price must be greater than zero."
                    });

                    return badRequest;
                }

                // Create menu item
                var menuItem =
                    await _tableStorageService
                        .CreateMenuItemAsync(request);

                // Return 201 Created
                var response =
                    req.CreateResponse(HttpStatusCode.Created);

                await response.WriteAsJsonAsync(menuItem);

                return response;
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "Invalid JSON received while creating menu item.");

                var badRequest =
                    req.CreateResponse(HttpStatusCode.BadRequest);

                await badRequest.WriteAsJsonAsync(new
                {
                    error = "The request body contains invalid JSON."
                });

                return badRequest;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error creating menu item.");

                var response =
                    req.CreateResponse(
                        HttpStatusCode.InternalServerError);

                await response.WriteAsJsonAsync(new
                {
                    error = "An unexpected error occurred while creating the menu item."
                });

                return response;
            }
        }
    }
}