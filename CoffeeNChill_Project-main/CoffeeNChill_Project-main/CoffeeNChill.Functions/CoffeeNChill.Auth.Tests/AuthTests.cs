using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CoffeeNChill.API.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

public sealed class MemoryUsers : IUserStore
{
    public ConcurrentDictionary<string, UserEntity> Users { get; } = new();
    public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
    public Task<UserEntity?> FindAsync(string email, CancellationToken ct) => Task.FromResult(Users.GetValueOrDefault(email.Trim().ToLowerInvariant()));
    public Task<bool> CreateAsync(UserEntity user, CancellationToken ct) => Task.FromResult(Users.TryAdd(user.RowKey.Trim().ToLowerInvariant(), user));
}
public sealed class DownstreamStub : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
}
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestKey = "test-only-32-byte-minimum-signing-key-not-for-deployment";
    public MemoryUsers Store { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Jwt:Key"] = TestKey, ["Jwt:Issuer"] = "CoffeeNChill.API", ["Jwt:Audience"] = "CoffeeNChill.Clients",
            ["SeedAdmin:Email"] = "", ["SeedAdmin:Password"] = ""
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserStore>(); services.AddSingleton<IUserStore>(Store);
            services.AddHttpClient("Functions").ConfigurePrimaryHttpMessageHandler(() => new DownstreamStub());
        });
    }
    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    public string Token(string role) => Services.GetRequiredService<JwtTokenService>().Create(new UserEntity { RowKey="person@example.com", FullName="Test User", PartitionKey=role }).Token;
}
public sealed class AuthTests
{
    [Fact] public void HashesAreSaltedAndVerifyOnlyCorrectPassword()
    {
        var user = new UserEntity(); var hasher = new PasswordHasher<UserEntity>();
        var first = hasher.HashPassword(user,"StrongPassword!123");
        Assert.NotEqual(first,hasher.HashPassword(user,"StrongPassword!123"));
        Assert.NotEqual(PasswordVerificationResult.Failed,hasher.VerifyHashedPassword(user,first,"StrongPassword!123"));
        Assert.Equal(PasswordVerificationResult.Failed,hasher.VerifyHashedPassword(user,first,"wrong"));
    }
    [Fact] public void TokenHasExpectedClaimsAndExpiry()
    {
        var settings=new JwtSettings { Key=ApiFactory.TestKey };
        var result=new JwtTokenService(settings).Create(new UserEntity { RowKey="staff@example.com",FullName="Staff",PartitionKey="Staff" });
        var handler=new JwtSecurityTokenHandler { MapInboundClaims=false };
        var principal=handler.ValidateToken(result.Token,settings.ValidationParameters(),out var validated);
        Assert.True(principal.IsInRole("Staff")); Assert.Equal("Staff",principal.Identity!.Name);
        Assert.Equal("staff@example.com",principal.FindFirstValue(ClaimTypes.Email));
        Assert.NotNull(principal.FindFirst("jti")); Assert.NotNull(principal.FindFirst("sub"));
        Assert.InRange(validated.ValidTo,DateTime.UtcNow.AddMinutes(59),DateTime.UtcNow.AddMinutes(61));
    }
    [Fact] public async Task RegistrationCannotElevateRoleAndLoginWorks()
    {
        using var app=new ApiFactory(); using var client=app.Client();
        var response=await client.PostAsJsonAsync("/api/auth/register",new { email="CUSTOMER@example.com",password="StrongPassword!123",fullName="Customer",role="Admin" });
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var body=await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("password",body,StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Customer",app.Store.Users["customer@example.com"].PartitionKey);
        Assert.NotEqual("StrongPassword!123",app.Store.Users["customer@example.com"].PasswordHash);
        var login=await client.PostAsJsonAsync("/api/auth/login",new { email="Customer@example.com",password="StrongPassword!123" });
        Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        var result=await login.Content.ReadFromJsonAsync<LoginResponse>(); Assert.NotNull(result);
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",result.Token);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/documents")).StatusCode);
    }
    [Theory]
    [InlineData("bad", "StrongPassword!123")]
    [InlineData("valid@example.com", "short")]
    [InlineData("bad#key@example.com", "StrongPassword!123")]
    public async Task InvalidRegistrationRejected(string email,string password)
    {
        using var app=new ApiFactory();using var client=app.Client();
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/auth/register",new {email,password,fullName="Test"})).StatusCode);
        Assert.Empty(app.Store.Users);
    }
    [Fact] public async Task DuplicateEmailIsRejectedAcrossRolesAndCase()
    {
        using var app=new ApiFactory();using var client=app.Client();
        await client.PostAsJsonAsync("/api/auth/register",new {email="user@example.com",password="StrongPassword!123",fullName="Test"});
        client.DefaultRequestHeaders.Authorization=new("Bearer",app.Token("Admin"));
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/auth/register-staff",new {email="USER@example.com",password="StrongPassword!123",fullName="Test",role="Staff"})).StatusCode);
        Assert.Single(app.Store.Users);
    }
    [Fact] public async Task InvalidCredentialsUseSameGenericResponse()
    {
        using var app=new ApiFactory();using var client=app.Client();
        await client.PostAsJsonAsync("/api/auth/register",new {email="user@example.com",password="StrongPassword!123",fullName="Test"});
        var a=await client.PostAsJsonAsync("/api/auth/login",new {email="user@example.com",password="wrong"});
        var b=await client.PostAsJsonAsync("/api/auth/login",new {email="missing@example.com",password="wrong"});
        Assert.Equal(HttpStatusCode.Unauthorized,a.StatusCode);Assert.Equal(a.StatusCode,b.StatusCode);
        Assert.Equal(await a.Content.ReadAsStringAsync(),await b.Content.ReadAsStringAsync());
    }
    public static IEnumerable<object[]> ProtectedRoutes()
    {
        foreach(var role in new[]{"Anonymous","Customer","Staff","Admin"})
        foreach(var route in new[]{("GET","/api/documents"),("GET","/api/documents/download/test.txt"),("POST","/api/documents"),("DELETE","/api/documents/test.txt"),("POST","/api/menu"),("PUT","/api/menu/Coffee/sku"),("DELETE","/api/menu/Coffee/sku"),("PUT","/api/orders/status")})
            yield return new object[]{role,route.Item1,route.Item2};
    }
    [Theory,MemberData(nameof(ProtectedRoutes))]
    public async Task ProtectedRouteMatrix(string role,string method,string route)
    {
        using var app=new ApiFactory();using var client=app.Client();
        if(role!="Anonymous")client.DefaultRequestHeaders.Authorization=new("Bearer",app.Token(role));
        using var request=new HttpRequestMessage(new HttpMethod(method),route);
        if (route == "/api/documents" && method == "POST")
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes("test"));
            file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            form.Add(file, "file", "test.txt");
            request.Content = form;
        }
        else if(method is "POST" or "PUT")request.Content=JsonContent.Create(new { });
        var response=await client.SendAsync(request);
        if(role=="Anonymous")Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
        else if(role=="Customer")Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        else
        {
            // Invalid empty bodies may be rejected by the teammate's DTOs after authorization succeeds.
            Assert.Contains(response.StatusCode,new[]{HttpStatusCode.OK,HttpStatusCode.BadRequest,HttpStatusCode.UnsupportedMediaType});
        }
    }
    [Theory]
    [InlineData("Anonymous",401)][InlineData("Customer",403)][InlineData("Staff",403)][InlineData("Admin",201)]
    public async Task OnlyAdminCanCreateStaff(string role,int status)
    {
        using var app=new ApiFactory();using var client=app.Client();
        if(role!="Anonymous")client.DefaultRequestHeaders.Authorization=new("Bearer",app.Token(role));
        var response=await client.PostAsJsonAsync("/api/auth/register-staff",new {email="staff@example.com",password="StrongPassword!123",fullName="Staff",role="Staff"});
        Assert.Equal(status,(int)response.StatusCode);
    }
    [Fact] public async Task PublicMenuAndOrderingDoNotRequireLogin()
    {
        using var app=new ApiFactory();using var client=app.Client();
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/menu")).StatusCode);
        var result=await client.PostAsJsonAsync("/api/orders/queue",new{});
        Assert.Contains(result.StatusCode,new[]{HttpStatusCode.OK,HttpStatusCode.BadRequest});
    }
    [Theory]
    [InlineData("signature")][InlineData("issuer")][InlineData("audience")][InlineData("expired")][InlineData("malformed")]
    public async Task InvalidTokensAreUnauthorized(string defect)
    {
        using var app=new ApiFactory();using var client=app.Client();
        var settings=app.Services.GetRequiredService<JwtSettings>();
        var token=new JwtSecurityToken(defect=="issuer"?"wrong":settings.Issuer,defect=="audience"?"wrong":settings.Audience,
            new[]{new Claim(ClaimTypes.Role,"Admin")},DateTime.UtcNow.AddHours(-2),
            defect=="expired"?DateTime.UtcNow.AddMinutes(-5):DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(defect=="signature"?new string('x',64):settings.Key)),SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization=new("Bearer",defect=="malformed"?"invalid":new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/documents")).StatusCode);
    }
    [Fact] public async Task SwaggerContainsBearerScheme()
    {
        using var app=new ApiFactory();using var client=app.Client();
        var response=await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Contains("Bearer",await response.Content.ReadAsStringAsync());
    }
}
