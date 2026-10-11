using CoffeeNChill.Functions.Constants;
using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Models;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CoffeeNChill.Functions.Functions.Orders
{
    /// <summary>
    /// Consumes order-processing-queue: writes the order as "Received", then
    /// simulates the barista lifecycle through to "Collected".
    /// </summary>
    public class ProcessOrderQueueFunction
    {
        private const int DefaultStepDelaySeconds = 3;

        private readonly IOrderService _orderService;
        private readonly int _stepDelaySeconds;
        private readonly ILogger<ProcessOrderQueueFunction> _logger;

        public ProcessOrderQueueFunction(
            IOrderService orderService,
            IConfiguration configuration,
            ILogger<ProcessOrderQueueFunction> logger)
        {
            _orderService = orderService;
            _logger = logger;

            _stepDelaySeconds = int.TryParse(configuration["OrderStatusStepDelaySeconds"], out int configured)
                ? configured
                : DefaultStepDelaySeconds;
        }

        [Function("ProcessOrderQueue")]
        public async Task Run(
            [QueueTrigger(StorageNames.OrdersQueue, Connection = "AzureWebJobsStorage")] string messageText,
            FunctionContext context)
        {
            string messageId = GetBindingData(context, "Id");
            string dequeueCount = GetBindingData(context, "DequeueCount");

            _logger.LogInformation(
                "Processing order queue message {MessageId}, dequeue count {DequeueCount}.",
                messageId,
                dequeueCount);

            OrderMessage? order;
            try
            {
                order = JsonSerializer.Deserialize<OrderMessage>(
                    messageText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                // Logged at Error with the raw body, then rethrown: the
                // runtime retries up to host.json's maxDequeueCount, after
                // which this message moves to order-processing-queue-poison
                // for ProcessPoisonOrders to pick up.
                _logger.LogError(
                    ex,
                    "Malformed order message {MessageId}. Raw body: {RawBody}",
                    messageId,
                    messageText);
                throw;
            }

            if (order is null || string.IsNullOrWhiteSpace(order.OrderId))
            {
                _logger.LogError(
                    "Order message {MessageId} is missing required fields (OrderId). Raw body: {RawBody}",
                    messageId,
                    messageText);
                throw new InvalidOperationException(
                    $"Order message {messageId} is missing required fields (OrderId).");
            }

            var (entity, wasRedelivery) = await _orderService.CreateAsync(order);

            if (wasRedelivery)
            {
                // Idempotent path: this OrderId was already written by an
                // earlier invocation of the same message (a redelivery, most
                // likely caused by the lifecycle loop below running past
                // host.json's 10s visibilityTimeout - see docs/CONTRACTS.md
                // for the full trade-off). Resume from whatever status it is
                // already at instead of restarting the lifecycle.
                _logger.LogWarning(
                    "Redelivery detected for order {OrderId} (message {MessageId}); resuming from status {Status}.",
                    entity.RowKey,
                    messageId,
                    entity.Status);
            }
            else
            {
                _logger.LogInformation("Order {OrderId} received and written to the Orders table.", entity.RowKey);
            }

            int currentIndex = Array.IndexOf(OrderService.StatusOrder, entity.Status);

            for (int i = currentIndex + 1; i < OrderService.StatusOrder.Length; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(_stepDelaySeconds));

                string nextStatus = OrderService.StatusOrder[i];
                var result = await _orderService.UpdateStatusAsync(entity.PartitionKey, entity.RowKey, nextStatus);

                if (result.Outcome == OrderStatusUpdateOutcome.ConcurrencyConflict)
                {
                    // A concurrent invocation (another redelivery of this
                    // same message) already advanced this order further.
                    // That invocation owns the rest of the lifecycle now -
                    // stopping here is correct, not a failure.
                    _logger.LogWarning(
                        "Order {OrderId} was advanced concurrently, most likely by a redelivered copy of this message; stopping this invocation's lifecycle loop.",
                        entity.RowKey);
                    return;
                }

                if (result.Outcome != OrderStatusUpdateOutcome.Success)
                {
                    // Not expected on this path: the index loop only ever
                    // requests the canonical next status, which UpdateStatusAsync
                    // always accepts as a valid forward transition unless the
                    // order was deleted out from under it.
                    _logger.LogError(
                        "Unexpected outcome {Outcome} advancing order {OrderId} to {Status}: {Error}",
                        result.Outcome,
                        entity.RowKey,
                        nextStatus,
                        result.Error);
                    return;
                }

                entity = result.Order!;
                _logger.LogInformation(
                    "Order {OrderId} advanced to {Status} at {At}.",
                    entity.RowKey,
                    nextStatus,
                    entity.StatusUpdatedAt);
            }
        }

        private static string GetBindingData(FunctionContext context, string key)
        {
            return context.BindingContext.BindingData.TryGetValue(key, out var value) && value is not null
                ? value.ToString() ?? "(unknown)"
                : "(unknown)";
        }
    }
}
