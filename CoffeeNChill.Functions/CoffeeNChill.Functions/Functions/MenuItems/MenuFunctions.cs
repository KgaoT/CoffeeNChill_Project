using Azure;
using CoffeeNChill.Functions.DTOs;
using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace CoffeeNChill.Functions.Functions.MenuItems;

public class MenuFunctions(IMenuItemService menuService)
{
    [Function("CreateMenuItem")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "menu")] HttpRequest req)
    {
        var request = await req.ReadFromJsonAsync<CreateMenuItemRequest>();
        if (request is null || string.IsNullOrWhiteSpace(request.Category) ||
            string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Name) ||
            request.Price < 0)
            return new BadRequestObjectResult(new { message = "Category, id and name are required, and price cannot be negative." });

        var item = new MenuItem
        {
            PartitionKey = request.Category.Trim(),
            RowKey = request.Id.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Price = request.Price,
            IsAvailable = request.IsAvailable
        };

        try
        {
            await menuService.CreateAsync(item);
            return new CreatedResult($"/api/menu/{Uri.EscapeDataString(item.PartitionKey)}/{Uri.EscapeDataString(item.RowKey)}", item);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            return new ConflictObjectResult(new { message = "A menu item with this category and id already exists." });
        }
    }

    [Function("GetAllMenuItems")]
    public async Task<IActionResult> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu")] HttpRequest req) =>
        new OkObjectResult(await menuService.GetAllAsync());

    [Function("GetMenuItemsByCategory")]
    public async Task<IActionResult> GetByCategory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu/category/{category}")] HttpRequest req,
        string category) => new OkObjectResult(await menuService.GetByCategoryAsync(category));

    [Function("UpdateMenuItem")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "menu/{category}/{id}")] HttpRequest req,
        string category,
        string id)
    {
        var request = await req.ReadFromJsonAsync<UpdateMenuItemRequest>();
        if (request is null || (request.Price is null && request.IsAvailable is null) || request.Price < 0)
            return new BadRequestObjectResult(new { message = "Supply a non-negative price and/or isAvailable value." });

        var item = await menuService.GetAsync(category, id);
        if (item is null) return new NotFoundObjectResult(new { message = "Menu item not found." });

        if (request.Price.HasValue) item.Price = request.Price.Value;
        if (request.IsAvailable.HasValue) item.IsAvailable = request.IsAvailable.Value;
        await menuService.UpdateAsync(item);
        return new OkObjectResult(item);
    }

    [Function("DeleteMenuItem")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "menu/{category}/{id}")] HttpRequest req,
        string category,
        string id) => await menuService.DeleteAsync(category, id)
            ? new NoContentResult()
            : new NotFoundObjectResult(new { message = "Menu item not found." });
}
