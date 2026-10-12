using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace CoffeeNChill.Functions.Services
{
    public class OrderQueueService : IOrderQueueService
    {
        private const string QueueName = "order-processing-queue";

        private readonly QueueClient _queueClient;
        private readonly ILogger<OrderQueueService> _logger;

        public OrderQueueService(
            IConfiguration configuration,
            ILogger<OrderQueueService> logger)
        {
            _logger = logger;

            string connectionString =
                configuration["AzureWebJobsStorage"]
                ?? throw new InvalidOperationException(
                    "AzureWebJobsStorage connection string is not configured.");

            QueueClientOptions options = new QueueClientOptions
            {
                MessageEncoding = QueueMessageEncoding.Base64
            };

            _queueClient = new QueueClient(
                connectionString,
                QueueName,
                options);
        }

        public async Task SendOrderAsync(Order order)
        {
            try
            {
                // Create the queue if it does not already exist
                await _queueClient.CreateIfNotExistsAsync();

                // SERIALIZATION:
                // Convert the C# Order object into JSON
                string jsonMessage = JsonSerializer.Serialize(order);

                // Send the JSON message to Azure Storage Queue
                await _queueClient.SendMessageAsync(jsonMessage);

                _logger.LogInformation(
                    "Order {OrderId} was successfully added to queue {QueueName}.",
                    order.OrderId,
                    QueueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to add order {OrderId} to queue {QueueName}.",
                    order.OrderId,
                    QueueName);

                throw;
            }
        }
    }
}
