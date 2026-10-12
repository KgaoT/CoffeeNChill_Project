using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Models;

namespace CoffeeNChill.Functions.Services
{
    /// <summary>
    /// Checks that every item on an order exists in the Menu table, using the
    /// same lookup the menu endpoints use. Returns one error per unknown item.
    /// </summary>
    public class OrderMenuChecker
    {
        private readonly ITableStorageService _menuService;

        public OrderMenuChecker(ITableStorageService menuService)
        {
            _menuService = menuService;
        }

        public async Task<IReadOnlyList<string>> FindUnknownItemsAsync(OrderMessage order)
        {
            var errors = new List<string>();

            // Look each distinct Category/SKU pair up once, even if the
            // order repeats it.
            var lines = order.Items
                .Select(i => (i.Category, i.SKU))
                .Distinct();

            foreach (var (category, sku) in lines)
            {
                var menuItem = await _menuService.GetMenuItemAsync(category, sku);

                if (menuItem is null)
                    errors.Add($"Menu item not found: category '{category}', SKU '{sku}'.");
                else if (!menuItem.IsAvailable)
                    errors.Add($"Menu item unavailable: category '{category}', SKU '{sku}'.");
            }

            return errors;
        }
    }
}