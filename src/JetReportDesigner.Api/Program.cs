using FluentValidation;
using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Core.Serialization;
using JetReportDesigner.Core.Validation;
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
