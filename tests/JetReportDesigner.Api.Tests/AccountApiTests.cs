using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JetReportDesigner.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JetReportDesigner.Api.Tests;

/// <summary>Sign-up, sign-in, lockout, password reset, profile and invitations against a real database.</summary>
public abstract class AccountApiTestsBase(DatabaseFixture fixture)
{
    private const string Password = "Test-Passw0rd!";

    private WebApplicationFactory<Program> CreateFactory(int? rateLimit = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Storage:Provider", fixture.Provider);
            builder.UseSetting("Storage:ConnectionString", fixture.ConnectionString);
            builder.UseSetting("Storage:MigrateOnStartup", "true");
            builder.UseTestJwt();
            if (rateLimit is { } limit)
            {
                builder.UseSetting("Auth:RateLimitPerMinute", limit.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        });

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static Task<HttpResponseMessage> Login(HttpClient c, string email, string password) =>
        c.PostAsJsonAsync("/api/auth/login", new { email, password });

    [Fact]
    public async Task Sign_up_reports_problems_per_field_in_the_users_language()
    {
        if (!fixture.Available)
        {
            return;
        }

        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("tr");

        var weak = await client.PostAsJsonAsync("/api/auth/register", new { email = NewEmail(), password = "abc", organizationName = "Org" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        var errors = (await Json(weak)).GetProperty("errors").GetProperty("password").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("En az 8 karakter kullanın.", errors);
        Assert.Contains("En az bir rakam (0-9) ekleyin.", errors);

        var email = NewEmail();
        var ok = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, organizationName = "Org", displayName = "Ayşe Yılmaz" });
        ok.EnsureSuccessStatusCode();
        Assert.Equal("Ayşe Yılmaz", (await Json(ok)).GetProperty("user").GetProperty("displayName").GetString());

        var duplicate = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, organizationName = "Other" });
        Assert.Contains("zaten var", (await Json(duplicate)).GetProperty("errors").GetProperty("email")[0].GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_and_a_reset_link_unlocks_it()
    {
        if (!fixture.Available)
        {
            return;
        }

        await using var factory = CreateFactory();
        var designer = await factory.CreateDesignerClientAsync();
        var invite = await Json(await designer.PostAsJsonAsync("/api/tenant/invites", new { role = "Viewer" }));
        var anon = factory.CreateClient();
        var email = NewEmail();
        (await anon.PostAsJsonAsync("/api/auth/register", new { email, password = Password, inviteCode = invite.GetProperty("code").GetString() })).EnsureSuccessStatusCode();

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Login(anon, email, "Wrong-Passw0rd")).StatusCode);
        }

        Assert.Equal((HttpStatusCode)423, (await Login(anon, email, "Wrong-Passw0rd")).StatusCode);
        Assert.Equal((HttpStatusCode)423, (await Login(anon, email, Password)).StatusCode); // even the right password waits

        var team = await Json(await designer.GetAsync("/api/auth/users"));
        var member = team.EnumerateArray().Single(u => u.GetProperty("email").GetString() == email);
        Assert.True(member.GetProperty("lockedOut").GetBoolean());

        // The Designer hands over a reset link; using it sets a new password and lifts the lock.
        var link = (await Json(await designer.PostAsync($"/api/auth/users/{member.GetProperty("id").GetGuid()}/reset-link", null)))
            .GetProperty("url").GetString()!;
        var query = System.Web.HttpUtility.ParseQueryString(new Uri(link).Query);
        Assert.Equal(email, query["email"]);
        var reset = await anon.PostAsJsonAsync("/api/auth/reset-password", new { email, token = query["token"], newPassword = "Brand-New-Passw0rd" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        (await Login(anon, email, "Brand-New-Passw0rd")).EnsureSuccessStatusCode();

        // The link works once.
        var again = await anon.PostAsJsonAsync("/api/auth/reset-password", new { email, token = query["token"], newPassword = "Another-Passw0rd" });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_answers_the_same_for_known_and_unknown_addresses()
    {
        if (!fixture.Available)
        {
            return;
        }

        await using var factory = CreateFactory();
        var anon = factory.CreateClient();
        var email = NewEmail();
        (await anon.PostAsJsonAsync("/api/auth/register", new { email, password = Password, organizationName = "Org" })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Accepted, (await anon.PostAsJsonAsync("/api/auth/forgot-password", new { email })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await anon.PostAsJsonAsync("/api/auth/forgot-password", new { email = NewEmail() })).StatusCode);
    }

    [Fact]
    public async Task People_manage_their_own_name_and_password()
    {
        if (!fixture.Available)
        {
            return;
        }

        await using var factory = CreateFactory();
        var anon = factory.CreateClient();
        var email = NewEmail();
        var auth = await Json(await anon.PostAsJsonAsync("/api/auth/register", new { email, password = Password, organizationName = "Org" }));
        var me = factory.CreateClient();
        me.DefaultRequestHeaders.Authorization = new("Bearer", auth.GetProperty("token").GetString());

        var renamed = await Json(await me.PutAsJsonAsync("/api/auth/me", new { displayName = "  Mehmet Özdemir " }));
        Assert.Equal("Mehmet Özdemir", renamed.GetProperty("displayName").GetString());

        var wrong = await me.PostAsJsonAsync("/api/auth/me/password", new { currentPassword = "nope", newPassword = "Another-Passw0rd" });
        Assert.True((await Json(wrong)).GetProperty("errors").TryGetProperty("currentPassword", out _));

        var weak = await me.PostAsJsonAsync("/api/auth/me/password", new { currentPassword = Password, newPassword = "short" });
        Assert.True((await Json(weak)).GetProperty("errors").TryGetProperty("password", out _));

        Assert.Equal(HttpStatusCode.NoContent, (await me.PostAsJsonAsync("/api/auth/me/password", new { currentPassword = Password, newPassword = "Another-Passw0rd" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(anon, email, Password)).StatusCode);
        (await Login(anon, email, "Another-Passw0rd")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task An_invite_link_shows_where_it_leads_and_signing_up_with_it_joins_the_team()
    {
        if (!fixture.Available)
        {
            return;
        }

        await using var factory = CreateFactory();
        var designer = await factory.CreateDesignerClientAsync();
        var org = (await Json(await designer.GetAsync("/api/tenant"))).GetProperty("name").GetString();
        Assert.False((await Json(await designer.GetAsync("/api/tenant/capabilities"))).GetProperty("emailConfigured").GetBoolean());

        var guest = NewEmail();
        var invite = await Json(await designer.PostAsJsonAsync("/api/tenant/invites", new { role = "Viewer", email = guest }));
        var code = invite.GetProperty("code").GetString()!;
        Assert.False(invite.GetProperty("emailSent").GetBoolean()); // no mail account: the link is shown to copy instead
        Assert.EndsWith($"/join/{code}", invite.GetProperty("link").GetString(), StringComparison.Ordinal);

        var anon = factory.CreateClient();
        var preview = await Json(await anon.GetAsync($"/api/auth/invites/{code.ToLowerInvariant()}"));
        Assert.Equal(org, preview.GetProperty("organizationName").GetString());
        Assert.Equal(guest, preview.GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/auth/invites/NOTACODE")).StatusCode);

        (await anon.PostAsJsonAsync("/api/auth/register", new { email = guest, password = Password, inviteCode = code, displayName = "Ayşe" })).EnsureSuccessStatusCode();
        var team = await Json(await designer.GetAsync("/api/auth/users"));
        var joined = team.EnumerateArray().Single(u => u.GetProperty("email").GetString() == guest);
        Assert.Equal("Ayşe", joined.GetProperty("displayName").GetString());
        Assert.Equal("Viewer", joined.GetProperty("roles")[0].GetString());
        Assert.NotEqual(JsonValueKind.Null, joined.GetProperty("lastLoginAtUtc").ValueKind);

        // Inviting someone who already has an account is caught up front.
        var taken = await designer.PostAsJsonAsync("/api/tenant/invites", new { role = "Viewer", email = guest });
        Assert.True((await Json(taken)).GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Sign_in_attempts_are_rate_limited_per_address()
    {
        if (!fixture.Available)
        {
            return;
        }

        await using var factory = CreateFactory(rateLimit: 3);
        var anon = factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Login(anon, NewEmail(), "x")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login(anon, NewEmail(), "x")).StatusCode);
    }
}

public sealed class SqlServerAccountApiTests(SqlServerDatabaseFixture fixture)
    : AccountApiTestsBase(fixture), IClassFixture<SqlServerDatabaseFixture>
{
}

public sealed class PostgreSqlAccountApiTests(PostgreSqlDatabaseFixture fixture)
    : AccountApiTestsBase(fixture), IClassFixture<PostgreSqlDatabaseFixture>
{
}
