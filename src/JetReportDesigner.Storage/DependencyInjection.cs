using JetReportDesigner.Storage.Connections;
using JetReportDesigner.Storage.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JetReportDesigner.Storage;

public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="JetReportDbContext"/> and repositories against the provider
    /// named in <paramref name="options"/>. Provider implementations live in the
    /// per-provider migrations assemblies and are passed in by the composition root.
    /// </summary>
    public static IServiceCollection AddJetReportStorage(
        this IServiceCollection services,
        StorageOptions options,
        params IStorageProvider[] providers)
    {
        ArgumentNullException.ThrowIfNull(options);

        var provider = Array.Find(providers, p => p.Provider == options.Provider)
            ?? throw new InvalidOperationException(
                $"No storage provider is registered for '{options.Provider}'. Available: "
                + string.Join(", ", providers.Select(p => p.Provider)));

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("Storage:ConnectionString is not configured.");
        }

        services.TryAddSingletonTimeProvider();
        services.AddDbContext<JetReportDbContext>(builder => provider.Configure(builder, options.ConnectionString));
        services.AddScoped<IConnectionRepository, ConnectionRepository>();

        if (options.ReportStore.Equals("filesystem", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.FileSystemPath))
            {
                throw new InvalidOperationException("Storage:FileSystemPath is required when Storage:ReportStore is 'filesystem'.");
            }

            var root = options.FileSystemPath;
            services.AddSingleton<IReportRepository>(sp =>
                new FileSystemReportRepository(root, sp.GetRequiredService<TimeProvider>()));
        }
        else
        {
            services.AddScoped<IReportRepository, ReportRepository>();
        }

        return services;
    }

    /// <summary>Applies any pending migrations. Call from the composition root when <see cref="StorageOptions.MigrateOnStartup"/> is set.</summary>
    public static async Task MigrateJetReportStorageAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<JetReportDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
