namespace CoffeeNChill.API.DTOs.Menu
{
    public class CreateMenuItemRequest
    {
        public string Category { get; set; } = string.Empty;

        public string SKU { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public double Price { get; set; }

        public bool IsAvailable { get; set; } = true;
    }
}
