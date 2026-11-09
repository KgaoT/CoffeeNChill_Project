using Azure;
using Azure.Data.Tables;
namespace CoffeeNChill.API.Auth;
/// <summary>A password-hashed user profile. PartitionKey is role; RowKey is normalized email.</summary>
public sealed class UserEntity : ITableEntity
{
    /// <summary>Role partition: Customer, Staff or Admin.</summary>
    public string PartitionKey { get; set; } = "Customer";
    /// <summary>Normalized email used as the profile key.</summary>
    public string RowKey { get; set; } = "";
    /// <summary>Salted PBKDF2 hash; never return this in API responses.</summary>
    public string PasswordHash { get; set; } = "";
    /// <summary>Display name for the authenticated user.</summary>
    public string FullName { get; set; } = "";
    /// <summary>UTC account creation timestamp.</summary>
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Azure-managed entity timestamp.</summary>
    public DateTimeOffset? Timestamp { get; set; }
    /// <summary>Azure optimistic-concurrency token.</summary>
    public ETag ETag { get; set; }
}
