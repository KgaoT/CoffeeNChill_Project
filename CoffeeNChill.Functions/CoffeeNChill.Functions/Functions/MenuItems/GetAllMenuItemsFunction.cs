using Azure;
using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace CoffeeNChill.Functions.Functions.MenuItems
{
    public class GetAllMenuItemsFunction
    {
        private readonly ITableStorageService _tableStorageService;

        private readonly ILogger<GetAllMenuItemsFunction> _logger;

        public GetAllMenuItemsFunction(
        ITableStorageService tableStorageService,
        ILogger<GetAllMenuItemsFunction> logger)
        {
            _tableStorageService = tableStorageService;
            _logger = logger;
        }

        [Function("GetAllMenuItems")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(
            AuthorizationLevel.Function,
            "get",
            Route = "menuitems")]
            HttpRequestData req)
        {
            _logger.LogInformation("Retrieving all menu items.");

            try
            {
                var menuItems = await _tableStorageService.GetAllMenuItemsAsync();

                HttpResponseData response =
                    req.CreateResponse(HttpStatusCode.OK);

                await response.WriteAsJsonAsync(menuItems);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving menu items. ");
                HttpResponseData response =
                    req.CreateResponse(HttpStatusCode.InternalServerError);

                await response.WriteStringAsync(
                    "An error occurred while retrieving menu items. ");
                return response;
            }
        }

    }
}
