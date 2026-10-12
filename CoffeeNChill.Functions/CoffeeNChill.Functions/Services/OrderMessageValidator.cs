using CoffeeNChill.Functions.Models;

namespace CoffeeNChill.Functions.Services
{
    /// <summary>
    /// Structural checks on an order message before it is written to the
    /// Orders table. Returns every problem found so a rejected order can be
    /// logged with a complete reason. Whether each SKU exists on the menu is
    /// checked separately against the Menu table.
    /// </summary>
    public static class OrderMessageValidator
    {
        public static IReadOnlyList<string> Validate(OrderMessage? order)
        {
            var errors = new List<string>();

            if (order is null)
            {
                errors.Add("Order message is empty.");
                return errors;
            }

            // The producer allows anonymous orders, so a missing name is
            // valid; only a blank name is rejected.
            if (order.CustomerName is not null && string.IsNullOrWhiteSpace(order.CustomerName))
                errors.Add("CustomerName cannot be blank when provided.");

            if (string.IsNullOrWhiteSpace(order.CustomerName))
                errors.Add("CustomerName is missing.");

            if (order.OrderTimestamp == default)
                errors.Add("OrderTimestamp is missing.");

            if (order.Items is null || order.Items.Count == 0)
            {
                errors.Add("Order has no items.");
                return errors;
            }

            for (int i = 0; i < order.Items.Count; i++)
            {
                var item = order.Items[i];

                if (string.IsNullOrWhiteSpace(item.SKU))
                    errors.Add($"Item {i + 1}: SKU is missing.");

                if (string.IsNullOrWhiteSpace(item.Category))
                    errors.Add($"Item {i + 1}: Category is missing.");

                if (item.Quantity <= 0)
                    errors.Add($"Item {i + 1}: Quantity must be greater than zero.");
            }

            return errors;
        }
    }
}