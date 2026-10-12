using CoffeeNChill.Functions.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;

namespace CoffeeNChill.Functions.Functions.MenuItems
{
    public class GetMenuItemsByCategoryFunction
    {
        private readonly ITableStorageService _tableStorageService;

        private readonly ILogger<GetMenuItemsByCategoryFunction> _logger;

        public GetMenuItemsByCategoryFunction(
            ITableStorageService tableStorageService,
            ILogger<GetMenuItemsByCategoryFunction> logger)
        {
            _tableStorageService = tableStorageService;
            _logger = logger;
        }

        [Function("GetMenuItemsByCategory")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(
                AuthorizationLevel.Anonymous,
                "get",
                Route = "menu/category/{category}")]
            HttpRequestData req,
            string category)
        {
            _logger.LogInformation(
                "Retrieving menu items by category. Category: {Category}",
                category);

            try
            {
                if (string.IsNullOrWhiteSpace(category))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Category is required." });
                    return badRequest;
                }

                var menuItems = await _tableStorageService.GetMenuItemsByCategoryAsync(category);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(menuItems);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving menu items by category.");

                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = "An unexpected error occurred while retrieving menu items." });
                return response;
            }
        }
    }
}
