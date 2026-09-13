using CoffeeNChill.Functions.DTOs;
using CoffeeNChill.Functions.Models;

namespace CoffeeNChill.Functions.Interfaces
{
    public interface ITableStorageService
    {
        // Used by CreateMenuItemFunction
        Task<MenuItem> CreateMenuItemAsync(CreateMenuItemRequest request);

        // Used by GetAllMenuItemsFunction
        Task<IEnumerable<MenuItem>> GetAllMenuItemsAsync();

        // Needed for your GetMenuItemsByCategory requirement
        Task<IEnumerable<MenuItem>> GetMenuItemsByCategoryAsync(string category);

        // Used by GetMenuItemFunction (single item lookup)
        Task<MenuItem?> GetMenuItemAsync(string category, string sku);

        // Used by UpdateMenuItemFunction
        Task<MenuItem?> UpdateMenuItemAsync(string category, string sku, UpdateMenuItemRequest request);

        // Used by DeleteMenuItemFunction
        Task<bool> DeleteMenuItemAsync(string category, string sku);
    }
}
