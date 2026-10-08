using Azure.Storage.Queues;
using CoffeeNChill.Functions.Constants;
using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Models;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace CoffeeNChill.Functions.Services
{
    public class QueueService : IQueueService
    {
        private readonly QueueClient _queueClient;

        public QueueService(IConfiguration configuration)
        {
            string connectionString =
                configuration["AzureWebJobsStorage"]
                ?? throw new InvalidOperationException(
                    "AzureWebJobsStorage connection string is missing.");

            _queueClient = new QueueClient(
                connectionString,
                StorageNames.OrdersQueue,
                new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });

            _queueClient.CreateIfNotExists();
        }

        public async Task EnqueueOrderAsync(OrderMessage order)
        {
            string messageJson = JsonSerializer.Serialize(order);
            await _queueClient.SendMessageAsync(messageJson);
        }
    }
}
