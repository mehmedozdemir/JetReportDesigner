using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Core.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using JetReportDesigner.Storage;

namespace JetReportDesigner.Api.Tests;

public sealed class ConnectionsApiTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly JsonSerializerOptions Json = ReportJson.Apply(new JsonSerializerOptions());

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("Storage:Provider", fixture.Provider);
            b.UseSetting("Storage:ConnectionString", fixture.ConnectionString);
            b.UseSetting("Storage:MigrateOnStartup", "true");
            b.UseTestJwt();
        });

    [Fact]
    public async Task Connection_Crud_RoundTrips_And_Never_Exposes_The_Secret()
    {
        if (!fixture.Available)
        {
            return;
        }

        await using var factory = CreateFactory();
        var client = await factory.CreateDesignerClientAsync();

        const string secret = "Server=db.internal;Database=Sales;User Id=sa;Password=SuperSecret123!";
        var create = await client.PostAsJsonAsync("/api/connections",
            new CreateConnectionRequest("Sales DB", "postgreSql", secret), Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ConnectionResponse>(Json);
        Assert.NotNull(created);
        Assert.Equal("Sales DB", created!.Name);
        Assert.Equal("postgreSql", created.Provider);

        var body = await create.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SuperSecret123", body, StringComparison.Ordinal);

        var list = await client.GetFromJsonAsync<List<ConnectionResponse>>("/api/connections", Json);
        Assert.Contains(list!, c => c.Id == created.Id);
        Assert.DoesNotContain("SuperSecret123", JsonSerializer.Serialize(list, Json), StringComparison.Ordinal);

        // Stored value is ciphertext, not the plaintext.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JetReportDbContext>();
            var row = await db.Connections.SingleAsync(c => c.Id == created.Id);
            Assert.DoesNotContain("SuperSecret123", row.EncryptedConnectionString, StringComparison.Ordinal);
            Assert.NotEqual(secret, row.EncryptedConnectionString);
        }

        var update = await client.PutAsJsonAsync($"/api/connections/{created.Id}",
            new UpdateConnectionRequest("Sales DB (renamed)", "postgreSql", null), Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<ConnectionResponse>(Json);
        Assert.Equal("Sales DB (renamed)", updated!.Name);

        var delete = await client.DeleteAsync($"/api/connections/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var afterDelete = await client.GetAsync($"/api/connections/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Create_Rejects_Unknown_Provider()
    {
        if (!fixture.Available)
        {
            return;
        }

        await using var factory = CreateFactory();
        var client = await factory.CreateDesignerClientAsync();

        var response = await client.PostAsJsonAsync("/api/connections",
            new CreateConnectionRequest("x", "mongo", "whatever"), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
