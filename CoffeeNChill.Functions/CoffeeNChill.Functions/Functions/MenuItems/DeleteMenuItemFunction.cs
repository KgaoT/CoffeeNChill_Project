using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace CoffeeNChill.Functions.Functions
{
    public class DeleteMenuItemFunction
    {
        private readonly ITableStorageService _tableStorageService;

        private readonly ILogger<DeleteMenuItemFunction> _logger;

        public DeleteMenuItemFunction(
        ITableStorageService tableStorageService,
        ILogger<DeleteMenuItemFunction> logger)
        {
            _tableStorageService = tableStorageService;
            _logger = logger;
        }

        [Function("DeleteMenuItem")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(
                AuthorizationLevel.Function,
                "delete",
                Route = "menuitems/{category}/{sku}")]
            HttpRequestData req,
            string category,
            string sku)
        {
            _logger.LogInformation(
                "Deleting menu item. Category: {Category}, SKU: {SKU}",
                category,
                sku);
            try
            {
                bool deleted = await _tableStorageService
                    .DeleteMenuItemAsync(category, sku);

                if (!deleted)
                {
                    var notFound =
                        req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Menu item not found. ");

                    return notFound;
                }
                return req.CreateResponse(HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting menu item.");

                var response =
                    req.CreateResponse(HttpStatusCode.InternalServerError);

                await response.WriteStringAsync(
                    "An error occurred while deleting the menu item.");

                return response;
            }
        }
    }
}
