using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace CoffeeNChill.Functions.Functions
{
    public class ProcessOrderQueue
    {
        private readonly IOrderTableService _orderTableService;
        private readonly ILogger<ProcessOrderQueue> _logger;

        public ProcessOrderQueue(
            IOrderTableService orderTableService,
            ILogger<ProcessOrderQueue> logger)
        {
            _orderTableService = orderTableService;
            _logger = logger;
        }

        [Function("ProcessOrderQueue")]
        public async Task Run(
            [QueueTrigger(
                "order-processing-queue",
                Connection = "AzureWebJobsStorage")]
            string queueMessage)
        {
            _logger.LogInformation(
                "Queue message received: {QueueMessage}",
                queueMessage);

            try
            {
                // DESERIALIZATION:
                // Convert JSON queue message back into an Order object
                Order? order =
                    JsonSerializer.Deserialize<Order>(
                        queueMessage,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                if (order == null)
                {
                    throw new InvalidOperationException(
                        "Queue message could not be deserialized into an Order.");
                }

                if (string.IsNullOrWhiteSpace(order.OrderId))
                {
                    throw new InvalidOperationException(
                        "Queue message does not contain a valid OrderId.");
                }

                string partitionKey =
                    order.OrderTimestamp.UtcDateTime
                        .ToString("yyyy-MM-dd");

                var orderEntity = new OrderEntity
                {
                    PartitionKey = partitionKey,
                    RowKey = order.OrderId,

                    OrderId = order.OrderId,
                    CustomerName = order.CustomerName,

                    SelectedItemSKUs =
                        JsonSerializer.Serialize(
                            order.SelectedItemSKUs),

                    TotalPrice =
                        Convert.ToDouble(order.TotalPrice),

                    OrderTimestamp =
                        order.OrderTimestamp,

                    Status = "Received",

                    LastUpdated =
                        DateTimeOffset.UtcNow
                };

                // Create order in Azure Table
                await _orderTableService
                    .CreateOrderAsync(orderEntity);

                _logger.LogInformation(
                    "Order {OrderId} status: Received.",
                    order.OrderId);

                // Simulate barista preparing the order
                await Task.Delay(2000);

                await _orderTableService
                    .UpdateOrderStatusAsync(
                        partitionKey,
                        order.OrderId,
                        "Preparing");

                _logger.LogInformation(
                    "Order {OrderId} status: Preparing.",
                    order.OrderId);

                // Simulate preparation time
                await Task.Delay(2000);

                await _orderTableService
                    .UpdateOrderStatusAsync(
                        partitionKey,
                        order.OrderId,
                        "Ready");

                _logger.LogInformation(
                    "Order {OrderId} status: Ready.",
                    order.OrderId);

                // Simulate collection
                await Task.Delay(2000);

                await _orderTableService
                    .UpdateOrderStatusAsync(
                        partitionKey,
                        order.OrderId,
                        "Collected");

                _logger.LogInformation(
                    "Order {OrderId} status: Collected.",
                    order.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process queue message: {QueueMessage}",
                    queueMessage);

                // IMPORTANT:
                // Re-throw the exception so Azure Functions knows
                // that queue processing failed.
                throw;
            }
        }
    }
}
