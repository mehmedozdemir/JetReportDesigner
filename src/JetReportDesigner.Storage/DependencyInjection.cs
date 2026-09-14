using JetReportDesigner.Storage.Assets;
using JetReportDesigner.Storage.Connections;
using JetReportDesigner.Storage.Email;
using JetReportDesigner.Storage.Entities;
using JetReportDesigner.Storage.Folders;
using JetReportDesigner.Storage.Repositories;
using JetReportDesigner.Storage.Sharing;
using JetReportDesigner.Storage.Tenancy;
using Microsoft.AspNetCore.Identity;
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
        services.AddScoped<ISqlQueryRepository, SqlQueryRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantInviteRepository, TenantInviteRepository>();
        services.AddScoped<IFolderRepository, FolderRepository>();
        services.AddScoped<IReportShareRepository, ReportShareRepository>();
        services.AddScoped<ISmtpSettingsRepository, SmtpSettingsRepository>();

        if (options.ReportStore.Equals("filesystem", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.FileSystemPath))
            {
                throw new InvalidOperationException("Storage:FileSystemPath is required when Storage:ReportStore is 'filesystem'.");
            }

            var root = options.FileSystemPath;
            // Scoped, not Singleton: the tenant subfolder depends on the current request's
            // ICurrentTenant, which is itself scoped.
            services.AddScoped<IReportRepository>(sp =>
                new FileSystemReportRepository(root, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ICurrentTenant>()));
        }
        else
        {
            services.AddScoped<IReportRepository, ReportRepository>();
        }

        return services;
    }

    /// <summary>
    /// Registers ASP.NET Core Identity (users + the fixed <see cref="AppRole.Designer"/> /
    /// <see cref="AppRole.Viewer"/> roles) against <see cref="JetReportDbContext"/>. No cookie
    /// sign-in or UI is configured — the API layer exchanges a validated password for a JWT.
    /// </summary>
    public static IServiceCollection AddJetReportIdentity(this IServiceCollection services)
    {
        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<JetReportDbContext>();

        return services;
    }

    /// <summary>Applies any pending migrations. Call from the composition root when <see cref="StorageOptions.MigrateOnStartup"/> is set.</summary>
    public static async Task MigrateJetReportStorageAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<JetReportDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>Ensures the <see cref="AppRole.Designer"/> and <see cref="AppRole.Viewer"/> roles
    /// exist. Call once at startup, after migrations.</summary>
    public static async Task SeedJetReportRolesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        foreach (var name in new[] { AppRole.Designer, AppRole.Viewer })
        {
            if (!await roles.RoleExistsAsync(name))
            {
                await roles.CreateAsync(new AppRole { Name = name });
            }
        }
    }

    /// <summary>
    /// The tenancy migration backfills every pre-existing row (users, reports, connections, …)
    /// with <see cref="Guid.Empty"/> as their TenantId, since that data predates tenants
    /// entirely. This ensures a matching "Tenant" row exists for it, so an upgraded
    /// single-tenant install keeps working with a real (if implicit) organization instead of
    /// a dangling id. Call once at startup, after migrations. A no-op on a fresh database
    /// (nothing will actually reference Guid.Empty).
    /// </summary>
    public static async Task SeedDefaultTenantAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<JetReportDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        if (!await db.Tenants.AnyAsync(t => t.Id == Guid.Empty, cancellationToken))
        {
            db.Tenants.Add(new Entities.Tenant { Id = Guid.Empty, Name = "Default Organization", CreatedAtUtc = clock.GetUtcNow().UtcDateTime });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
