namespace CoffeeNChill.API.DTOs.Menu
{
    public class UpdateMenuItemRequest
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public double? Price { get; set; }

        public bool? IsAvailable { get; set; }
    }
}
