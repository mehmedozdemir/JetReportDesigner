namespace JetReportDesigner.Storage.Tenancy;

public sealed record TenantInfo(Guid Id, string Name, DateTime CreatedAtUtc);

public interface ITenantRepository
{
    /// <summary>Creates a brand-new tenant (organization/workspace) and returns its id.</summary>
    Task<Guid> CreateAsync(string name, CancellationToken cancellationToken);

    Task<TenantInfo?> GetAsync(Guid id, CancellationToken cancellationToken);
}
