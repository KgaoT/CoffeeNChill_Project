namespace CoffeeNChill.Functions.Constants
{
    // Shared storage identifiers so every function/service and every group
    // member's code refers to the same table/container/queue by one constant
    // instead of a repeated string literal.
    public static class StorageNames
    {
        public const string MenuItemsTable = "MenuItems";

        public const string StaffDocsContainer = "staff-docs";

        // Consumed by ProcessOrderQueue. Named to match the brief exactly
        // ("order-processing-queue"), not the placeholder name this project
        // started with.
        public const string OrdersQueue = "order-processing-queue";

        // The Functions runtime creates and manages this automatically once a
        // QueueTrigger message exceeds host.json's maxDequeueCount - it is
        // always "<queue-name>-poison" by convention. Declared here, not
        // created by any service, so ProcessPoisonOrders can bind to the
        // exact same literal the runtime uses.
        public const string OrdersPoisonQueue = "order-processing-queue-poison";

        // Written by ProcessOrderQueue.
        public const string OrdersTable = "Orders";

        // Written by ProcessPoisonOrders for messages that exhausted retries.
        public const string FailedOrdersTable = "FailedOrders";
    }
}
