using System.Text;
using FluentValidation;
using JetReportDesigner.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using JetReportDesigner.Core.Serialization;
using JetReportDesigner.Core.Validation;
using JetReportDesigner.DataSources;
using JetReportDesigner.DataSources.Http;
using JetReportDesigner.DataSources.Json;
using JetReportDesigner.Rendering;
using JetReportDesigner.Rendering.Engines;
using JetReportDesigner.Storage;
using JetReportDesigner.Storage.Migrations.Oracle;
using JetReportDesigner.Storage.Migrations.PostgreSql;
using JetReportDesigner.Storage.Migrations.SqlServer;
using JetReportDesigner.Storage.Entities;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, config) => config
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));

// --- Storage (provider chosen by the Storage:Provider setting) ---
var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
    ?? new StorageOptions();
builder.Services.AddSingleton(storageOptions);
builder.Services.AddJetReportStorage(
    storageOptions,
    new SqlServerStorageProvider(),
    new PostgreSqlStorageProvider(),
    new OracleStorageProvider());
builder.Services.AddJetReportIdentity();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<JetReportDesigner.Storage.Tenancy.ICurrentTenant, CurrentTenant>();

// --- Auth: password login exchanged for a JWT (no cookies) ---
builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<JwtTokenService>();

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtSecret = jwtSection["Secret"] ?? string.Empty;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.Designer, policy => policy.RequireRole(AppRole.Designer))
    // Every endpoint requires a signed-in user (Designer or Viewer) unless it opts out
    // with [AllowAnonymous] — currently only /api/auth/register and /api/auth/login.
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// --- Web ---
builder.Services
    .AddControllers()
    .AddJsonOptions(o => ReportJson.Apply(o.JsonSerializerOptions));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddValidatorsFromAssemblyContaining<ReportDefinitionValidator>();

// Connection strings for registered database connections are encrypted at rest.
// In a container, set DataProtection:KeyPath to a mounted volume so the key ring
// survives restarts (otherwise stored secrets become undecryptable).
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("JetReportDesigner");
var keyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(keyPath))
{
    Directory.CreateDirectory(keyPath);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
}
builder.Services.AddSingleton<
    JetReportDesigner.Storage.Connections.IConnectionSecretProtector,
    JetReportDesigner.Api.Infrastructure.DataProtectionSecretProtector>();
builder.Services.AddSingleton<JetReportDesigner.Api.Infrastructure.Email.IEmailSender, JetReportDesigner.Api.Infrastructure.Email.SmtpEmailSender>();

// --- Data sources + rendering ---
var restOptions = builder.Configuration.GetSection(RestSourceOptions.SectionName).Get<RestSourceOptions>()
    ?? new RestSourceOptions();
var ssrfGuard = new SsrfGuard(restOptions.AllowedHosts);
builder.Services.AddSingleton(ssrfGuard);
builder.Services.AddSingleton<IDataSourceReader, JsonDataSourceReader>();
builder.Services.AddSingleton<IDataSourceReader>(_ =>
    new RestDataSourceReader(RestHttp.CreateClient(ssrfGuard, restOptions), ssrfGuard));
builder.Services.AddScoped<JetReportDesigner.DataSources.Sql.ConnectionStringResolver>(sp =>
{
    var connections = sp.GetRequiredService<JetReportDesigner.Storage.Connections.IConnectionRepository>();
    return (id, ct) => connections.ResolveAsync(id, ct);
});
builder.Services.AddScoped<IDataSourceReader, JetReportDesigner.DataSources.Sql.SqlDataSourceReader>();
builder.Services.AddScoped<ReportDataResolver>();
builder.Services.AddSingleton<IPdfRenderer, MigraDocPdfRenderer>();

// Images referenced by a report (asset:{id}, data: URIs, http(s) URLs) are resolved
// to bytes before rendering; external fetches reuse the SSRF-guarded HTTP client.
var imageHttpClient = RestHttp.CreateClient(ssrfGuard, restOptions);
builder.Services.AddScoped<JetReportDesigner.Rendering.IRenderImageResolver>(sp =>
    new JetReportDesigner.Api.Infrastructure.RenderImageResolver(
        sp.GetRequiredService<JetReportDesigner.Storage.Assets.IAssetRepository>(),
        imageHttpClient));

// A subreport element embeds another saved report by id.
builder.Services.AddScoped<JetReportDesigner.Rendering.ISubreportResolver, JetReportDesigner.Api.Infrastructure.SubreportResolver>();
builder.Services.AddScoped<ReportRenderService>();
builder.Services.AddSingleton<JetReportDesigner.Api.Localization.IApiStrings, JetReportDesigner.Api.Localization.ApiStrings>();
builder.Services.AddSingleton<JetReportDesigner.Api.Jobs.RunningJobs>();
builder.Services.AddHostedService<JetReportDesigner.Api.Jobs.ReportJobProcessor>();
builder.Services.AddHostedService<JetReportDesigner.Api.Jobs.ReportScheduleTrigger>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<JetReportDesigner.Storage.JetReportDbContext>("storage");

const string SpaCorsPolicy = "spa";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(SpaCorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (storageOptions.MigrateOnStartup)
{
    await app.Services.MigrateJetReportStorageAsync();
}

await app.Services.SeedJetReportRolesAsync();
await app.Services.SeedDefaultTenantAsync();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var supportedCultures = new[] { "en", "tr" };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(JetReportDesigner.Api.Localization.ApiStrings.DefaultCulture)
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

app.UseCors(SpaCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

// Serve the built SPA from wwwroot when present (single-container deployment).
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();
// The SPA shell itself (and its login screen) must load before the user has a token.
app.MapFallbackToFile("index.html").AllowAnonymous();

await app.RunAsync();

/// <summary>Exposed so the integration test project can drive the app with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
