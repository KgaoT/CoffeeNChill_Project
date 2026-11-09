using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
namespace CoffeeNChill.API.Auth;
/// <summary>Validated signing configuration shared by token issuance and bearer validation.</summary>
public sealed class JwtSettings
{
    /// <summary>Secret signing key supplied through private configuration.</summary>
    public string Key { get; set; } = "";
    /// <summary>Expected token issuer.</summary>
    public string Issuer { get; set; } = "CoffeeNChill.API";
    /// <summary>Expected token audience.</summary>
    public string Audience { get; set; } = "CoffeeNChill.Clients";
    /// <summary>Token lifetime in minutes.</summary>
    public int ExpiryMinutes { get; set; } = 60;
    /// <summary>Rejects missing or invalid token configuration.</summary>
    public void Validate()
    {
        if (Encoding.UTF8.GetByteCount(Key) < 32 || string.IsNullOrWhiteSpace(Issuer) ||
            string.IsNullOrWhiteSpace(Audience) || ExpiryMinutes is < 1 or > 120)
            throw new InvalidOperationException("Configure Jwt:Key (at least 32 bytes), Issuer, Audience and ExpiryMinutes (1-120).");
    }
    /// <summary>Returns strict bearer token validation settings.</summary>
    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
        ValidateIssuer = true, ValidIssuer = Issuer,
        ValidateAudience = true, ValidAudience = Audience,
        ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
        ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role
    };
}
/// <summary>Issues signed, expiring JWTs for authenticated users.</summary>
public sealed class JwtTokenService(JwtSettings settings)
{
    /// <summary>Creates a signed token for a verified user.</summary>
    public LoginResponse Create(UserEntity user)
    {
        var now = DateTimeOffset.UtcNow;
        var expiry = now.AddMinutes(settings.ExpiryMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.RowKey),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.RowKey),
            new Claim(ClaimTypes.Role, user.PartitionKey)
        };
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            now.UtcDateTime, expiry.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expiry, user.PartitionKey, user.FullName);
    }
}
