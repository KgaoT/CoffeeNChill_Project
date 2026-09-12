using CoffeeNChill.Functions.Models;

namespace CoffeeNChill.Functions.Interfaces;

public interface IMenuItemService
{
    Task CreateAsync(MenuItem item);
    Task<IReadOnlyList<MenuItem>> GetAllAsync();
    Task<IReadOnlyList<MenuItem>> GetByCategoryAsync(string category);
    Task<MenuItem?> GetAsync(string category, string id);
    Task UpdateAsync(MenuItem item);
    Task<bool> DeleteAsync(string category, string id);
}
