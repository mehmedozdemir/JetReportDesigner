using JetReportDesigner.Storage.Folders;

namespace JetReportDesigner.Api.Contracts;

public sealed record FolderResponse(Guid Id, string Name, Guid? ParentFolderId, DateTime CreatedAtUtc)
{
    public static FolderResponse From(FolderInfo f) => new(f.Id, f.Name, f.ParentFolderId, f.CreatedAtUtc);
}

public sealed record CreateFolderRequest(string Name, Guid? ParentFolderId);

public sealed record RenameFolderRequest(string Name);

public sealed record MoveFolderRequest(Guid? ParentFolderId);

public sealed record SetReportFolderRequest(Guid? FolderId);
