using Azure;
using Azure.Data.Tables;
using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Models;
using Microsoft.Extensions.Configuration;

namespace CoffeeNChill.Functions.Services;

public class MenuItemService : IMenuItemService
{
    private const string TableName = "MenuItems";
    private readonly TableClient _tableClient;

    public MenuItemService(IConfiguration configuration)
    {
        var connectionString = configuration["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("AzureWebJobsStorage connection string is missing.");

        _tableClient = new TableClient(connectionString, TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task CreateAsync(MenuItem item) => await _tableClient.AddEntityAsync(item);

    public async Task<IReadOnlyList<MenuItem>> GetAllAsync()
    {
        var items = new List<MenuItem>();
        await foreach (var item in _tableClient.QueryAsync<MenuItem>()) items.Add(item);
        return items;
    }

    public async Task<IReadOnlyList<MenuItem>> GetByCategoryAsync(string category)
    {
        var items = new List<MenuItem>();
        await foreach (var item in _tableClient.QueryAsync<MenuItem>(x => x.PartitionKey == category))
            items.Add(item);
        return items;
    }

    public async Task<MenuItem?> GetAsync(string category, string id)
    {
        var response = await _tableClient.GetEntityIfExistsAsync<MenuItem>(category, id);
        return response.HasValue ? response.Value : null;
    }

    public async Task UpdateAsync(MenuItem item) =>
        await _tableClient.UpdateEntityAsync(item, ETag.All, TableUpdateMode.Merge);

    public async Task<bool> DeleteAsync(string category, string id)
    {
        try
        {
            await _tableClient.DeleteEntityAsync(category, id);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }
}
