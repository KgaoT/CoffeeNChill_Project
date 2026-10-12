using System.Collections.Generic;

namespace CoffeeNChill.Functions.DTOs
{
    public class CreateOrderRequest
    {
        public string? OrderId { get; set; }

        public string? CustomerName { get; set; }

        public List<string>? SelectedItemSKUs { get; set; }

        public decimal TotalPrice { get; set; }
    }
}
