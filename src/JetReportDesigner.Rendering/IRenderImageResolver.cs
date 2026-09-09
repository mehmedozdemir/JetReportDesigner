namespace JetReportDesigner.Rendering;

/// <summary>An image's decoded bytes and MIME type.</summary>
public sealed record ResolvedImage(byte[] Bytes, string ContentType);

/// <summary>
/// Resolves an image reference used in a report (<c>asset:{id}</c>, an http(s) URL or
/// a data URI) to bytes. Implemented in the host so the rendering project stays free
/// of storage and HTTP concerns; returns null when the reference cannot be resolved.
/// </summary>
public interface IRenderImageResolver
{
    Task<ResolvedImage?> ResolveAsync(string source, CancellationToken cancellationToken);
}

/// <summary>Resolves nothing — the default when no host resolver is supplied (e.g. in tests).</summary>
public sealed class NullImageResolver : IRenderImageResolver
{
    public static readonly NullImageResolver Instance = new();

    public Task<ResolvedImage?> ResolveAsync(string source, CancellationToken cancellationToken) =>
        Task.FromResult<ResolvedImage?>(null);
}
