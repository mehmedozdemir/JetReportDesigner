namespace JetReportDesigner.Storage.Folders;

public sealed record FolderInfo(Guid Id, string Name, Guid? ParentFolderId, DateTime CreatedAtUtc);

public enum FolderDeleteResult
{
    Deleted,
    NotFound,
    /// <summary>The folder still has subfolders or reports filed in it.</summary>
    NotEmpty,
}

public interface IFolderRepository
{
    /// <summary>Every folder in the tenant — the caller builds the tree from ParentFolderId.</summary>
    Task<IReadOnlyList<FolderInfo>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Null if <paramref name="parentFolderId"/> doesn't exist in this tenant.</summary>
    Task<FolderInfo?> CreateAsync(string name, Guid? parentFolderId, CancellationToken cancellationToken);

    Task<FolderInfo?> RenameAsync(Guid id, string name, CancellationToken cancellationToken);

    Task<FolderDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>reportId -> folderId for every report currently filed in a folder (a report with
    /// no entry is at the root).</summary>
    Task<IReadOnlyDictionary<Guid, Guid>> GetReportFolderMapAsync(CancellationToken cancellationToken);

    /// <summary>Files a report under a folder, or back at the root when <paramref name="folderId"/>
    /// is null. False if <paramref name="folderId"/> doesn't exist in this tenant.</summary>
    Task<bool> SetReportFolderAsync(Guid reportId, Guid? folderId, CancellationToken cancellationToken);
}
