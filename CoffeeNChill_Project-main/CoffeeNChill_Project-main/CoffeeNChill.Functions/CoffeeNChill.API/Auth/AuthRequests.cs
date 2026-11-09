using System.ComponentModel.DataAnnotations;
namespace CoffeeNChill.API.Auth;
/// <summary>Validates email addresses that can also be used as Azure Table row keys.</summary>
public sealed class TableEmailAttribute : ValidationAttribute
{
    /// <summary>Checks email syntax and Azure Table key restrictions.</summary>
    public override bool IsValid(object? value) => value is string email &&
        email.Length <= 254 && new EmailAddressAttribute().IsValid(email.Trim()) &&
        !email.Any(c => c is '/' or '\\' or '#' or '?' || char.IsControl(c));
}
/// <summary>Public registration always creates a Customer; no client-supplied role is accepted.</summary>
public class RegisterRequest
{
    /// <summary>Email address identifying the account.</summary>
    [Required, TableEmail(ErrorMessage = "Enter a valid email without /, backslash, #, ? or control characters.")]
    public string Email { get; set; } = "";
    /// <summary>Transient submitted password; never persist this value.</summary>
    [Required, StringLength(128, MinimumLength = 12)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^a-zA-Z0-9]).+$", ErrorMessage = "Use upper and lowercase letters, a number and a symbol.")]
    public string Password { get; set; } = "";
    /// <summary>Display name for the authenticated user.</summary>
    [Required, StringLength(100, MinimumLength = 1)]
    public string FullName { get; set; } = "";
}
/// <summary>Only an authenticated Admin may create privileged accounts.</summary>
public sealed class StaffRegisterRequest : RegisterRequest
{
    /// <summary>Privileged role requested by an authorized Admin.</summary>
    [Required, RegularExpression("^(Staff|Admin)$")]
    public string Role { get; set; } = "Staff";
}
/// <summary>Credentials are accepted in the body only and are never returned.</summary>
public sealed class LoginRequest
{
    [Required, TableEmail] public string Email { get; set; } = "";
    [Required, StringLength(128)] public string Password { get; set; } = "";
}
/// <summary>Safe user data without storage metadata or password hashes.</summary>
public sealed record UserResponse(string Email, string FullName, string Role, DateTimeOffset CreatedDate);
/// <summary>A short-lived bearer token and its expiry.</summary>
public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt, string Role, string FullName);
