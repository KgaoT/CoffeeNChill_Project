namespace CoffeeNChill.Functions.DTOs
{
    public class CreateOrderRequest
    {
        public string? CustomerName { get; set; }

        public List<OrderItemRequest> Items { get; set; } = new();
    }

    public class OrderItemRequest
    {
        // Must match an existing MenuItem's PartitionKey/RowKey.
        public string Category { get; set; } = string.Empty;

        public string SKU { get; set; } = string.Empty;

        public int Quantity { get; set; }
    }
}
