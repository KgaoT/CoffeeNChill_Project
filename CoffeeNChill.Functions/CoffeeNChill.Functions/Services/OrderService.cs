using Azure;
using Azure.Data.Tables;
using CoffeeNChill.Functions.Constants;
using CoffeeNChill.Functions.Interfaces;
using CoffeeNChill.Functions.Models;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace CoffeeNChill.Functions.Services
{
    public class OrderService : IOrderService
    {
        /// <summary>
        /// Canonical lifecycle order. "Forward-only" means the target
        /// status's index must be strictly greater than the current status's
        /// index - jumping ahead (e.g. Received straight to Collected) is
        /// allowed, only moving sideways or backward is rejected. This is a
        /// deliberately looser reading of "forward-only" than "must be the
        /// very next status", chosen so a demo or a legitimate fast-tracked
        /// order isn't blocked by a rule the brief doesn't explicitly require
        /// to be step-by-step; see docs/CONTRACTS.md.
        /// </summary>
        public static readonly string[] StatusOrder = { "Received", "Preparing", "Ready", "Collected" };

        private readonly TableClient _ordersTable;
        private readonly TableClient _failedOrdersTable;

        public OrderService(IConfiguration configuration)
        {
            string connectionString =
                configuration["AzureWebJobsStorage"]
                ?? throw new InvalidOperationException(
                    "AzureWebJobsStorage connection string is missing.");

            _ordersTable = new TableClient(connectionString, StorageNames.OrdersTable);
            _ordersTable.CreateIfNotExists();

            _failedOrdersTable = new TableClient(connectionString, StorageNames.FailedOrdersTable);
            _failedOrdersTable.CreateIfNotExists();
        }

        public async Task<(OrderEntity Order, bool WasRedelivery)> CreateAsync(OrderMessage message)
        {
            var entity = new OrderEntity
            {
                PartitionKey = message.OrderTimestamp.UtcDateTime.ToString("yyyy-MM-dd"),
                RowKey = message.OrderId,
                CustomerName = message.CustomerName,
                SelectedItemSKUsJson = JsonSerializer.Serialize(message.SelectedItemSKUs),
                TotalPrice = message.TotalPrice,
                OrderTimestamp = message.OrderTimestamp,
                Status = StatusOrder[0],
                StatusUpdatedAt = DateTimeOffset.UtcNow,
                StatusHistoryJson = JsonSerializer.Serialize(new[]
                {
                    new OrderStatusHistoryEntry { Status = StatusOrder[0], At = DateTimeOffset.UtcNow }
                })
            };

            try
            {
                await _ordersTable.AddEntityAsync(entity);
                return (entity, WasRedelivery: false);
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                // Redelivery of the same queue message: the row already
                // exists. Return it as-is so the caller resumes the
                // lifecycle from its current status instead of restarting.
                var existing = await _ordersTable.GetEntityAsync<OrderEntity>(entity.PartitionKey, entity.RowKey);
                return (existing.Value, WasRedelivery: true);
            }
        }

        public async Task<OrderEntity?> GetAsync(string orderDate, string orderId)
        {
            try
            {
                var response = await _ordersTable.GetEntityAsync<OrderEntity>(orderDate, orderId);
                return response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        public async Task<List<OrderEntity>> ListAsync(string? date)
        {
            var orders = new List<OrderEntity>();

            var query = string.IsNullOrWhiteSpace(date)
                ? _ordersTable.QueryAsync<OrderEntity>()
                : _ordersTable.QueryAsync<OrderEntity>(o => o.PartitionKey == date);

            await foreach (var order in query)
            {
                orders.Add(order);
            }

            return orders;
        }

        public async Task<OrderStatusUpdateResult> UpdateStatusAsync(string orderDate, string orderId, string newStatus)
        {
            OrderEntity? entity;
            try
            {
                var response = await _ordersTable.GetEntityAsync<OrderEntity>(orderDate, orderId);
                entity = response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return new OrderStatusUpdateResult(OrderStatusUpdateOutcome.NotFound, null, "Order not found.");
            }

            int currentIndex = Array.IndexOf(StatusOrder, entity.Status);
            int targetIndex = Array.IndexOf(StatusOrder, newStatus);

            if (targetIndex < 0)
            {
                return new OrderStatusUpdateResult(
                    OrderStatusUpdateOutcome.InvalidTransition,
                    entity,
                    $"'{newStatus}' is not a recognized status. Expected one of: {string.Join(", ", StatusOrder)}.");
            }

            if (targetIndex <= currentIndex)
            {
                return new OrderStatusUpdateResult(
                    OrderStatusUpdateOutcome.InvalidTransition,
                    entity,
                    $"Cannot move from '{entity.Status}' to '{newStatus}': only forward transitions are allowed.");
            }

            ApplyStatus(entity, newStatus);

            try
            {
                var updateResponse = await _ordersTable.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace);
                entity.ETag = updateResponse.Headers.ETag ?? entity.ETag;
                return new OrderStatusUpdateResult(OrderStatusUpdateOutcome.Success, entity, null);
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                // Another write landed between our read and this write - most
                // likely a concurrent redelivery of the same queue message
                // already advancing this order. Not a failure the caller
                // should treat as an error; see ProcessOrderQueueFunction.
                return new OrderStatusUpdateResult(
                    OrderStatusUpdateOutcome.ConcurrencyConflict,
                    entity,
                    "The order was updated concurrently; this status change was not applied.");
            }
        }

        public async Task LogFailedOrderAsync(string messageId, string rawBody, string reason)
        {
            var entity = new TableEntity(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"), messageId)
            {
                { "RawBody", rawBody },
                { "Reason", reason },
                { "LoggedAt", DateTimeOffset.UtcNow }
            };

            await _failedOrdersTable.UpsertEntityAsync(entity);
        }

        private static void ApplyStatus(OrderEntity entity, string newStatus)
        {
            entity.Status = newStatus;
            entity.StatusUpdatedAt = DateTimeOffset.UtcNow;

            var history = JsonSerializer.Deserialize<List<OrderStatusHistoryEntry>>(entity.StatusHistoryJson)
                ?? new List<OrderStatusHistoryEntry>();
            history.Add(new OrderStatusHistoryEntry { Status = newStatus, At = entity.StatusUpdatedAt });
            entity.StatusHistoryJson = JsonSerializer.Serialize(history);
        }
    }
}
