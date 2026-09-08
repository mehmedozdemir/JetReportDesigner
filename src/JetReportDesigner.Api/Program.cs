using FluentValidation;
using JetReportDesigner.Api.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using JetReportDesigner.Core.Serialization;
using JetReportDesigner.Core.Validation;
using JetReportDesigner.DataSources;
using JetReportDesigner.DataSources.Http;
using JetReportDesigner.DataSources.Json;
using JetReportDesigner.Rendering;
using JetReportDesigner.Rendering.Engines;
using JetReportDesigner.Storage;
using JetReportDesigner.Storage.Migrations.PostgreSql;
using JetReportDesigner.Storage.Migrations.SqlServer;
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
    new PostgreSqlStorageProvider());

// --- Web ---
builder.Services
    .AddControllers()
    .AddJsonOptions(o => ReportJson.Apply(o.JsonSerializerOptions));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddValidatorsFromAssemblyContaining<ReportDefinitionValidator>();

// Connection strings for registered database connections are encrypted at rest.
builder.Services.AddDataProtection().SetApplicationName("JetReportDesigner");
builder.Services.AddSingleton<
    JetReportDesigner.Storage.Connections.IConnectionSecretProtector,
    JetReportDesigner.Api.Infrastructure.DataProtectionSecretProtector>();

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
builder.Services.AddScoped<ReportRenderService>();

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

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(SpaCorsPolicy);

// Serve the built SPA from wwwroot when present (single-container deployment).
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.MapFallbackToFile("index.html");

await app.RunAsync();

/// <summary>Exposed so the integration test project can drive the app with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
