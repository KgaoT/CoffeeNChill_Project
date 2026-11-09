using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
namespace CoffeeNChill.API.Auth;
/// <summary>Creates storage and optionally seeds the first Admin from private configuration.</summary>
public sealed class AuthInitializer(IServiceProvider services, IConfiguration config) : IHostedService
{
    /// <summary>Initializes user storage and optionally seeds the first Admin.</summary>
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IUserStore>();
        await store.InitializeAsync(ct);
        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];
        if (string.IsNullOrEmpty(email) && string.IsNullOrEmpty(password)) return;
        var request = new RegisterRequest { Email = email ?? "", Password = password ?? "", FullName = "Administrator" };
        if (!Validator.TryValidateObject(request, new ValidationContext(request), new List<ValidationResult>(), true))
            throw new InvalidOperationException("SeedAdmin configuration does not meet registration requirements.");
        var existing = await store.FindAsync(request.Email, ct);
        if (existing is not null)
        {
            if (existing.PartitionKey != "Admin") throw new InvalidOperationException("SeedAdmin email already belongs to a non-Admin. No promotion was performed.");
            return; // Never reset an existing Admin password on restart.
        }
        var user = new UserEntity { RowKey = request.Email.Trim().ToLowerInvariant(), PartitionKey = "Admin", FullName = request.FullName };
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<UserEntity>>().HashPassword(user, request.Password);
        if (!await store.CreateAsync(user, ct))
        {
            var concurrent = await store.FindAsync(user.RowKey, ct);
            if (concurrent?.PartitionKey != "Admin")
                throw new InvalidOperationException("Admin creation could not complete. Check email reservation state.");
        }
    }
    /// <summary>Completes shutdown; this initializer holds no background work.</summary>
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
