namespace CoffeeNChill.Functions.DTOs
{
    public class UpdateOrderStatusRequest
    {
        /// <summary>UTC date the order's PartitionKey is stored under ("yyyy-MM-dd").</summary>
        public string? OrderDate { get; set; }

        public string? OrderId { get; set; }

        /// <summary>One of "Received", "Preparing", "Ready", "Collected".</summary>
        public string? Status { get; set; }
    }
}
