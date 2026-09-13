using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.ConstrainedExecution;
using System.Text;

namespace CoffeeNChill.Functions.Functions.MenuItems
{
    public class GetMenuItemFunction
    {
        private readonly ITableStorageService _tableStorageService;

        private readonly ILogger<GetMenuItemFunction> _logger;

        public GetMenuItemFunction(
            ITableStorageService tableStorageService,
            ILogger<GetMenuItemFunction> logger)
        {
            _tableStorageService = tableStorageService;
            _logger = logger;
        }

        [Function("GetMenuItem")]

        public async Task<HttpResponseData> Run(
            [HttpTrigger(
                AuthorizationLevel.Function,
                "get",
                Route = "menuitems/{category}/{sku}")]
            HttpRequestData req,
            string category,
            string sku)

        {
            _logger.LogInformation(
            "Retrieving menu item. Category: {Category}, SKU: {SKU}",
            category,
            sku);
            {

                try
                {
                    // Validate category
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

                    // Validate SKU
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

                    // Retrieve menu item
                    var menuItem =
                        await _tableStorageService
                            .GetMenuItemAsync(category, sku);

                    // Menu item does not exist
                    if (menuItem == null)
                    {
                        var notFound =
                            req.CreateResponse(HttpStatusCode.NotFound);

                        await notFound.WriteAsJsonAsync(new
                        {
                            error = "Menu item not found."
                        });

                        return notFound;
                    }

                    // Successful response
                    var response =
                        req.CreateResponse(HttpStatusCode.OK);

                    await response.WriteAsJsonAsync(menuItem);

                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error retrieving menu item.");

                    var response =
                        req.CreateResponse(
                            HttpStatusCode.InternalServerError);

                    await response.WriteAsJsonAsync(new
                    {
                        error = "An unexpected error occurred while retrieving the menu item."
                    });

                    return response;
                }
            }
        }
    }
}

