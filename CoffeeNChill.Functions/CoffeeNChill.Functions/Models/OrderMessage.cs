namespace CoffeeNChill.Functions.Models
{
    // The queue message contract. This is what this branch's producer writes
    // and what Tetelo's ProcessOrderQueue trigger reads back out: field names
    // and types here are the shared agreement, see docs/CONTRACTS.md.
    public class OrderMessage
    {
        public string OrderId { get; set; } = string.Empty;

        public string? CustomerName { get; set; }

        public List<OrderMessageItem> Items { get; set; } = new();

        public DateTimeOffset QueuedAt { get; set; }
    }

    public class OrderMessageItem
    {
        public string Category { get; set; } = string.Empty;

        public string SKU { get; set; } = string.Empty;

        public int Quantity { get; set; }
    }
}
