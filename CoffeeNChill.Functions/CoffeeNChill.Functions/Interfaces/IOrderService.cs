using CoffeeNChill.Functions.Models;

namespace CoffeeNChill.Functions.Interfaces
{
    public enum OrderStatusUpdateOutcome
    {
        Success,
        NotFound,
        InvalidTransition,
        ConcurrencyConflict
    }

    /// <summary>
    /// Result of an attempted status change. Carries enough detail for an
    /// HTTP caller to pick a status code and for the queue trigger to decide
    /// whether to keep advancing or stop.
    /// </summary>
    public record OrderStatusUpdateResult(OrderStatusUpdateOutcome Outcome, OrderEntity? Order, string? Error);

    public interface IOrderService
    {
        /// <summary>
        /// Writes a new Orders row with Status "Received". If
        /// <paramref name="message"/>.OrderId already exists (a redelivered
        /// queue message), this does not throw: it returns the existing row
        /// instead, with <c>WasRedelivery</c> true, so the caller can resume
        /// the lifecycle from whatever status that row is already at.
        /// </summary>
        Task<(OrderEntity Order, bool WasRedelivery)> CreateAsync(OrderMessage message);

        Task<OrderEntity?> GetAsync(string orderDate, string orderId);

        /// <summary>All orders, optionally restricted to one UTC date ("yyyy-MM-dd").</summary>
        Task<List<OrderEntity>> ListAsync(string? date);

        /// <summary>
        /// Validates the transition is forward-only (status order: Received,
        /// Preparing, Ready, Collected - the target must be strictly later
        /// than the order's current status, not necessarily the very next
        /// one) and applies it with ETag optimistic concurrency.
        /// </summary>
        Task<OrderStatusUpdateResult> UpdateStatusAsync(string orderDate, string orderId, string newStatus);

        /// <summary>Records a poison-queue failure for visibility in Storage Explorer.</summary>
        Task LogFailedOrderAsync(string messageId, string rawBody, string reason);
    }
}
