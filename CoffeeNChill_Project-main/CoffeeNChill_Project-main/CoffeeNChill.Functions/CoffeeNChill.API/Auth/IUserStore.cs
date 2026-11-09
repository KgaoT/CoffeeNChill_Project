namespace CoffeeNChill.API.Auth;
/// <summary>Storage contract; creation must reject duplicate emails across all roles.</summary>
public interface IUserStore
{
    Task InitializeAsync(CancellationToken ct);
    Task<UserEntity?> FindAsync(string email, CancellationToken ct);
    Task<bool> CreateAsync(UserEntity user, CancellationToken ct);
}
