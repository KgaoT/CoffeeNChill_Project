namespace CoffeeNChill.API.DTOs.Orders
{
    public class OrderResponse
    {
        public string Message { get; set; } = string.Empty;

        public string OrderId { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTimeOffset OrderTimestamp { get; set; }
    }
}
