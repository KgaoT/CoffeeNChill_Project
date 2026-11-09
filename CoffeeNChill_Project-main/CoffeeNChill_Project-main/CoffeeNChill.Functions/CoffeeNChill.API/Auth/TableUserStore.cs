using Azure;
using Azure.Data.Tables;
namespace CoffeeNChill.API.Auth;
/// <summary>Stores profiles in Users and uses an atomic email reservation to prevent cross-role duplicates.</summary>
public sealed class TableUserStore : IUserStore
{
    private readonly TableClient users;
    private readonly TableClient emails;
    /// <summary>Connects to profile and email-index tables using private configuration.</summary>
    public TableUserStore(IConfiguration config)
    {
        var connection = config["AuthStorage:ConnectionString"]
            ?? throw new InvalidOperationException("Set AuthStorage__ConnectionString.");
        users = new TableClient(connection, "Users");
        emails = new TableClient(connection, "UserEmailIndex");
    }
    /// <summary>Creates required storage tables if they do not exist.</summary>
    public async Task InitializeAsync(CancellationToken ct)
    {
        await users.CreateIfNotExistsAsync(ct);
        await emails.CreateIfNotExistsAsync(ct);
    }
    /// <summary>Finds a unique account by normalized email.</summary>
    public async Task<UserEntity?> FindAsync(string email, CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();
        // Query existing profiles too, including ones predating the email index.
        var filter = TableClient.CreateQueryFilter($"RowKey eq {email}");
        UserEntity? found = null;
        await foreach (var user in users.QueryAsync<UserEntity>(filter, cancellationToken: ct))
        {
            if (found is not null) throw new InvalidOperationException("Duplicate user emails require administrator repair.");
            found = user;
        }
        return found;
    }
    /// <summary>Creates a profile only after reserving its email globally.</summary>
    public async Task<bool> CreateAsync(UserEntity user, CancellationToken ct)
    {
        user.RowKey = user.RowKey.Trim().ToLowerInvariant();
        if (await FindAsync(user.RowKey, ct) is not null) return false;
        try
        {
            // Add (not upsert) makes the reservation atomic across processes and roles.
            await emails.AddEntityAsync(new TableEntity("Email", user.RowKey)
            { ["Role"] = user.PartitionKey, ["CreatedDate"] = DateTimeOffset.UtcNow }, ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 409) { return false; }
        // Fail closed: retain the reservation after an ambiguous storage failure.
        // See AUTH-SETUP.md for recovery; never delete a possibly committed account's index.
        await users.AddEntityAsync(user, ct);
        return true;
    }
}
