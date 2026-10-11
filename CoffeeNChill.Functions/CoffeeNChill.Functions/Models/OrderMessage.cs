namespace CoffeeNChill.Functions.Models
{
    /// <summary>
    /// The queue message contract for <c>order-processing-queue</c>. Field
    /// names match the brief's sample payload exactly, since ProcessOrderQueue
    /// (and anyone inspecting the queue in Storage Explorer) depends on them.
    /// See docs/CONTRACTS.md for the full schema and the design choices below.
    /// </summary>
    public class OrderMessage
    {
        /// <summary>
        /// Server-generated, never client-supplied: "ORD-{yyyy}-{5-digit}".
        /// See QueueOrderFunction for the collision-safety trade-off.
        /// </summary>
        public string OrderId { get; set; } = string.Empty;

        public string? CustomerName { get; set; }

        /// <summary>
        /// Flat list of SKUs as the brief's sample shows it: one entry per
        /// unit ordered, so a quantity of 2 for "COF-001" appears twice. This
        /// keeps the field a faithful "what was selected" list without
        /// requiring a reader to cross-reference <see cref="Items"/> for
        /// quantities, while still being a plain string array.
        /// </summary>
        public List<string> SelectedItemSKUs { get; set; } = new();

        /// <summary>
        /// Computed server-side from MenuItems prices x quantity - never
        /// trusted from the client, which has no TotalPrice field to send in
        /// the first place. Rounded to 2 decimal places.
        /// </summary>
        public double TotalPrice { get; set; }

        /// <summary>UTC, set when the order was queued - not client-supplied.</summary>
        public DateTimeOffset OrderTimestamp { get; set; }

        /// <summary>
        /// Additional detail beyond the brief's five fields: quantities and
        /// categories per line, kept so ProcessOrderQueue doesn't have to
        /// infer quantity from repeated SKUs in <see cref="SelectedItemSKUs"/>.
        /// </summary>
        public List<OrderMessageItem> Items { get; set; } = new();
    }

    public class OrderMessageItem
    {
        public string Category { get; set; } = string.Empty;

        public string SKU { get; set; } = string.Empty;

        public int Quantity { get; set; }
    }
}
