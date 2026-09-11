using System.Net.Http.Headers;
using System.Net.Http.Json;
using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Storage.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace JetReportDesigner.Api.Tests;

/// <summary>
/// Every endpoint requires an authenticated caller once auth is wired in, so integration
/// tests need a client carrying a JWT before they can exercise the API.
/// </summary>
internal static class TestAuth
{
    /// <summary>32+ characters, as <c>JwtOptions.Secret</c> requires; test-only, never used outside this project.</summary>
    public const string JwtSecret = "integration-test-only-signing-secret-not-for-real-use";

    public static IWebHostBuilder UseTestJwt(this IWebHostBuilder builder) =>
        builder.UseSetting("Jwt:Secret", JwtSecret);

    /// <summary>
    /// Registers a fresh user, promotes it to Designer directly through <see cref="UserManager{TUser}"/>
    /// (registration only auto-grants Designer to the very first user ever created against a
    /// database, and these tests share one database across several <c>[Fact]</c>s), then logs in
    /// again so the returned token actually carries the Designer role claim.
    /// </summary>
    public static async Task<HttpClient> CreateDesignerClientAsync(this WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var email = $"designer-{Guid.NewGuid():N}@example.com";
        const string password = "Test-Passw0rd!";

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password));
        register.EnsureSuccessStatusCode();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await users.FindByEmailAsync(email) ?? throw new InvalidOperationException("Registered user not found.");
            var currentRoles = await users.GetRolesAsync(user);
            await users.RemoveFromRolesAsync(user, currentRoles);
            await users.AddToRoleAsync(user, AppRole.Designer);
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("Login did not return a token.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }
}
