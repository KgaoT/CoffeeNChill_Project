namespace CoffeeNChill.API.DTOs.Orders
{
    public class UpdateOrderStatusRequest
    {
        public string OrderId { get; set; } = string.Empty;

        public DateTimeOffset OrderTimestamp { get; set; }

        public string Status { get; set; } = string.Empty;
    }
}
