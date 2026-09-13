using Azure;
using Azure.Core;
using CoffeeNChill.Functions.DTOs;
using CoffeeNChill.Functions.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
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
                    Route = "menuitems")]
                    HttpRequestData req)
        {
            _logger.LogInformation("Creating a new menu item.");
            try
            {
                var request =
                    await JsonSerializer.DeserializeAsync<CreateMenuItemRequest>(
                        req.Body,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });


                if (request == null)
                {
                    var badRequest =
                        req.CreateResponse(HttpStatusCode.BadRequest);

                    await badRequest.WriteStringAsync(
                        "Invalid request body.");
                    return badRequest;
                }

                _logger.LogInformation($"Category = {request.Category}");
                _logger.LogInformation($"SKU = {request.SKU}");
                _logger.LogInformation($"Name = {request.Name} ");
                _logger.LogInformation($"Price = {request.Price}");
                _logger.LogInformation($"Price received = {request.Price}");

                var menuItem =
                    await _tableStorageService
                         .CreateMenuItemAsync(request);

                var response =
                    req.CreateResponse(HttpStatusCode.Created);

                await response.WriteAsJsonAsync(menuItem);

                return response;
            }

            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error creating menu item.");

                var response =
                    req.CreateResponse(
                        HttpStatusCode.InternalServerError);

                await response.WriteStringAsync(
                    "An error occurred while creating the menu item.");
                return response;
            }

        }
    }

}
