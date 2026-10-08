using CoffeeNChill.Functions.DTOs;
using CoffeeNChill.Functions.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CoffeeNChill.Functions.Functions.MenuItems
{
    public class UpdateMenuItemFunction
    {
        private readonly ITableStorageService _tableStorageService;
        private readonly ILogger<UpdateMenuItemFunction> _logger;

        public UpdateMenuItemFunction(
            ITableStorageService tableStorageService,
            ILogger<UpdateMenuItemFunction> logger)
        {
            _tableStorageService = tableStorageService;
            _logger = logger;
        }

        [Function("UpdateMenuItem")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(
                AuthorizationLevel.Anonymous,
                "put",
                Route = "menu/{category}/{sku}")]
            HttpRequestData req,
            string category,
            string sku)
        {
            _logger.LogInformation(
                "Updating menu item. Category: {Category}, SKU: {SKU}",
                category,
                sku);

            try
            {
                // Validate route parameters
                if (string.IsNullOrWhiteSpace(category))
                {
                    var badRequest =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badRequest.WriteAsJsonAsync(new
                    {
                        error = "Category is required."
                    });

                    return badRequest;
                }

                if (string.IsNullOrWhiteSpace(sku))
                {
                    var badRequest =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badRequest.WriteAsJsonAsync(new
                    {
                        error = "SKU is required."
                    });

                    return badRequest;
                }

                // Read and deserialize request body
                var request =
                    await JsonSerializer.DeserializeAsync<UpdateMenuItemRequest>(
                        req.Body,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                // Validate request body
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

                // Per the brief, this endpoint updates price and/or availability: at
                // least one updatable field must be supplied, but none is mandatory
                // on its own, and Name/Description are optional overrides.
                if (request.Name is null && request.Description is null
                    && request.Price is null && request.IsAvailable is null)
                {
                    var badRequest =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badRequest.WriteAsJsonAsync(new
                    {
                        error = "At least one of Name, Description, Price, or IsAvailable must be provided."
                    });

                    return badRequest;
                }

                // Validate Name, only if supplied
                if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
                {
                    var badRequest =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badRequest.WriteAsJsonAsync(new
                    {
                        error = "Name cannot be blank."
                    });

                    return badRequest;
                }

                // Validate Description, only if supplied
                if (request.Description is not null && string.IsNullOrWhiteSpace(request.Description))
                {
                    var badRequest =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badRequest.WriteAsJsonAsync(new
                    {
                        error = "Description cannot be blank."
                    });

                    return badRequest;
                }

                // Validate Price, only if supplied
                if (request.Price is <= 0)
                {
                    var badRequest =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badRequest.WriteAsJsonAsync(new
                    {
                        error = "Price must be greater than zero."
                    });

                    return badRequest;
                }

                // Update menu item
                var updatedMenuItem =
                    await _tableStorageService.UpdateMenuItemAsync(
                        category,
                        sku,
                        request);

                // Menu item does not exist
                if (updatedMenuItem == null)
                {
                    var notFound =
                        req.CreateResponse(HttpStatusCode.NotFound);

                    await notFound.WriteAsJsonAsync(new
                    {
                        error = "Menu item not found."
                    });

                    return notFound;
                }

                // Successful update
                var response =
                    req.CreateResponse(HttpStatusCode.OK);

                await response.WriteAsJsonAsync(updatedMenuItem);

                return response;
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "Invalid JSON received while updating menu item.");

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
                    "Error updating menu item.");

                var response =
                    req.CreateResponse(
                        HttpStatusCode.InternalServerError);

                await response.WriteAsJsonAsync(new
                {
                    error = "An unexpected error occurred while updating the menu item."
                });

                return response;
            }
        }
    }
}

