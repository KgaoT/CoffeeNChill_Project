using Azure;
using Azure.Data.Tables;

namespace CoffeeNChill.Functions.Models
{
    public class MenuItem : ITableEntity
    {
        // Table Storage requires PartitionKey and RowKey.
        // Category groups items together (PartitionKey);
        // SKU uniquely identifies an item within that category (RowKey).
        public string PartitionKey { get; set; } = default!; // Category
        public string RowKey { get; set; } = default!;       // SKU

        public string Category
        {
            get => PartitionKey;
            set => PartitionKey = value;
        }

        public string SKU
        {
            get => RowKey;
            set => RowKey = value;
        }

        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
        public double Price { get; set; }

        // Required by ITableEntity - managed automatically by the SDK
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
