using Azure;
using Azure.Data.Tables;

namespace CoffeeNChill.Functions.Models
{
    /// <summary>
    /// The Orders table row. PartitionKey is the order's UTC date
    /// ("yyyy-MM-dd", derived from <see cref="OrderTimestamp"/>) so a day's
    /// orders sit in one partition; RowKey is the OrderId.
    /// </summary>
    public class OrderEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;

        public string RowKey { get; set; } = string.Empty;

        public string? CustomerName { get; set; }

        /// <summary>
        /// JSON-serialized <c>string[]</c>: Table Storage has no native array
        /// type, so <see cref="OrderMessage.SelectedItemSKUs"/> is stored as
        /// a JSON string and deserialized back out on read.
        /// </summary>
        public string SelectedItemSKUsJson { get; set; } = "[]";

        public double TotalPrice { get; set; }

        /// <summary>When the order was originally queued (business time, UTC).</summary>
        public DateTimeOffset OrderTimestamp { get; set; }

        /// <summary>One of "Received", "Preparing", "Ready", "Collected".</summary>
        public string Status { get; set; } = string.Empty;

        public DateTimeOffset StatusUpdatedAt { get; set; }

        /// <summary>
        /// JSON-serialized list of <see cref="OrderStatusHistoryEntry"/>, one
        /// per status this order has passed through, oldest first.
        /// </summary>
        public string StatusHistoryJson { get; set; } = "[]";

        /// <summary>Storage-managed last-modified time; not the business OrderTimestamp above.</summary>
        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }
    }

    public class OrderStatusHistoryEntry
    {
        public string Status { get; set; } = string.Empty;

        public DateTimeOffset At { get; set; }
    }
}
