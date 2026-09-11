using System.Net.Http.Headers;
using System.Net.Http.Json;
using JetReportDesigner.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

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
    /// Registers a fresh user as the founder of a brand-new organization (which always makes
    /// the registering user its Designer, regardless of how many other users/tenants already
    /// exist in the shared test database).
    /// </summary>
    public static async Task<HttpClient> CreateDesignerClientAsync(this WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var email = $"designer-{Guid.NewGuid():N}@example.com";
        const string password = "Test-Passw0rd!";

        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, password, OrganizationName: $"Org {Guid.NewGuid():N}", InviteCode: null));
        register.EnsureSuccessStatusCode();
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("Register did not return a token.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }
}
