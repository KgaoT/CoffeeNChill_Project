namespace CoffeeNChill.Functions.Constants
{
    // Shared storage identifiers so every function/service and every group
    // member's code refers to the same table/container/queue by one constant
    // instead of a repeated string literal.
    public static class StorageNames
    {
        public const string MenuItemsTable = "MenuItems";

        public const string StaffDocsContainer = "staff-docs";

        // Consumed by Tetelo's ProcessOrderQueue trigger.
        public const string OrdersQueue = "orders-queue";

        // Written by Tetelo's ProcessOrderQueue trigger, not by this branch.
        public const string OrdersTable = "Orders";
    }
}
