using CoffeeNChill.Functions.Models;

namespace CoffeeNChill.Functions.Interfaces
{
    public interface IQueueService
    {
        Task EnqueueOrderAsync(OrderMessage order);
    }
}
