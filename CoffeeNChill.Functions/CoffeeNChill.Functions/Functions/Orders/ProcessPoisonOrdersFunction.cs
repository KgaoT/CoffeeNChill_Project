using CoffeeNChill.Functions.Constants;
using CoffeeNChill.Functions.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Functions.Orders
{
    /// <summary>
    /// Consumes order-processing-queue-poison: messages ProcessOrderQueue
    /// could not process after host.json's maxDequeueCount retries. Logs the
    /// full failure detail and records it in FailedOrders so it's visible in
    /// Storage Explorer rather than silently dropped.
    /// </summary>
    public class ProcessPoisonOrdersFunction
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<ProcessPoisonOrdersFunction> _logger;

        public ProcessPoisonOrdersFunction(IOrderService orderService, ILogger<ProcessPoisonOrdersFunction> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [Function("ProcessPoisonOrders")]
        public async Task Run(
            [QueueTrigger(StorageNames.OrdersPoisonQueue, Connection = "AzureWebJobsStorage")] string messageText,
            FunctionContext context)
        {
            string messageId = GetBindingData(context, "Id");
            string insertionTime = GetBindingData(context, "InsertionTime");
            string dequeueCount = GetBindingData(context, "DequeueCount");

            _logger.LogError(
                "Order message moved to poison queue. MessageId: {MessageId}, InsertionTime: {InsertionTime}, DequeueCount: {DequeueCount}, RawBody: {RawBody}",
                messageId,
                insertionTime,
                dequeueCount,
                messageText);

            // The original processing exception isn't available here - only
            // the Functions runtime's own retry mechanism saw it, not this
            // trigger. Reason records that limitation rather than guessing
            // at a specific cause; the function logs around InsertionTime
            // carry the actual exception from ProcessOrderQueue's attempts.
            await _orderService.LogFailedOrderAsync(
                messageId,
                messageText,
                "Moved to poison queue after exceeding host.json's maxDequeueCount; see ProcessOrderQueue's logs around the same InsertionTime for the original processing exception.");
        }

        private static string GetBindingData(FunctionContext context, string key)
        {
            return context.BindingContext.BindingData.TryGetValue(key, out var value) && value is not null
                ? value.ToString() ?? "(unknown)"
                : "(unknown)";
        }
    }
}
