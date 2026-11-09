using CoffeeNChill.API.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
namespace CoffeeNChill.API.Controllers;
/// <summary>Registration, authentication and current-user information.</summary>
[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(IUserStore store, IPasswordHasher<UserEntity> hasher, JwtTokenService tokens) : ControllerBase
{
    // A real hash ensures unknown-email attempts still perform password verification.
    private static readonly UserEntity Dummy = new();
    private static readonly string DummyHash = new PasswordHasher<UserEntity>().HashPassword(Dummy, Guid.NewGuid().ToString());
    /// <summary>Registers a Customer. Role fields in the JSON cannot grant privileges.</summary>
    [HttpPost("register"), AllowAnonymous]
    [ProducesResponseType(typeof(UserResponse), 201)]
    [ProducesResponseType(400), ProducesResponseType(409)]
    public Task<IActionResult> Register(RegisterRequest request, CancellationToken ct) => Create(request, "Customer", ct);
    /// <summary>Creates a Staff or Admin account; restricted to Admins.</summary>
    [HttpPost("register-staff"), Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserResponse), 201)]
    [ProducesResponseType(400), ProducesResponseType(401), ProducesResponseType(403), ProducesResponseType(409)]
    public Task<IActionResult> RegisterStaff(StaffRegisterRequest request, CancellationToken ct) => Create(request, request.Role, ct);
    private async Task<IActionResult> Create(RegisterRequest request, string role, CancellationToken ct)
    {
        var user = new UserEntity { RowKey = request.Email.Trim().ToLowerInvariant(), PartitionKey = role, FullName = request.FullName.Trim() };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        if (!await store.CreateAsync(user, ct)) return Conflict(new { message = "Registration could not be completed for this email." });
        return StatusCode(201, new UserResponse(user.RowKey, user.FullName, role, user.CreatedDate));
    }
    /// <summary>Validates credentials and returns a signed bearer token.</summary>
    [HttpPost("login"), AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(400), ProducesResponseType(401)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await store.FindAsync(request.Email, ct);
        var result = hasher.VerifyHashedPassword(user ?? Dummy, user?.PasswordHash ?? DummyHash, request.Password);
        if (user is null || result == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Invalid email or password." });
        Response.Headers.CacheControl = "no-store";
        return Ok(tokens.Create(user));
    }
    /// <summary>Returns the authenticated identity without password or storage fields.</summary>
    [HttpGet("me"), Authorize]
    [ProducesResponseType(200), ProducesResponseType(401)]
    public IActionResult Me() => Ok(new { email = User.FindFirstValue(ClaimTypes.Email), fullName = User.Identity?.Name, role = User.FindFirstValue(ClaimTypes.Role) });
}
