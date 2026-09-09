using JetReportDesigner.Storage.Assets;

namespace JetReportDesigner.Api.Contracts;

public sealed record AssetResponse(Guid Id, string FileName, string ContentType, int ByteLength, DateTime CreatedAtUtc)
{
    public static AssetResponse From(SavedAsset a) =>
        new(a.Id, a.FileName, a.ContentType, a.ByteLength, a.CreatedAtUtc);
}
