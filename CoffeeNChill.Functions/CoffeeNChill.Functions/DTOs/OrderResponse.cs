using CoffeeNChill.Functions.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoffeeNChill.Functions.DTOs
{
    /// <summary>
    /// Shapes an <see cref="OrderEntity"/> for API responses: the table row's
    /// JSON-string columns come back out as real arrays.
    /// </summary>
    public class OrderResponse
    {
        public string OrderId { get; set; } = string.Empty;

        public string OrderDate { get; set; } = string.Empty;

        public string? CustomerName { get; set; }

        public List<string> SelectedItemSKUs { get; set; } = new();

        public double TotalPrice { get; set; }

        public DateTimeOffset OrderTimestamp { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTimeOffset StatusUpdatedAt { get; set; }

        // Only the single-order GET includes this; the list endpoint omits
        // it to keep the response small, so it's left out of the JSON
        // entirely (rather than serialized as null) when not requested.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<OrderStatusHistoryEntry>? StatusHistory { get; set; }

        public static OrderResponse FromEntity(OrderEntity entity, bool includeHistory)
        {
            return new OrderResponse
            {
                OrderId = entity.RowKey,
                OrderDate = entity.PartitionKey,
                CustomerName = entity.CustomerName,
                SelectedItemSKUs = JsonSerializer.Deserialize<List<string>>(entity.SelectedItemSKUsJson) ?? new(),
                TotalPrice = entity.TotalPrice,
                OrderTimestamp = entity.OrderTimestamp,
                Status = entity.Status,
                StatusUpdatedAt = entity.StatusUpdatedAt,
                StatusHistory = includeHistory
                    ? JsonSerializer.Deserialize<List<OrderStatusHistoryEntry>>(entity.StatusHistoryJson)
                    : null
            };
        }
    }
}
