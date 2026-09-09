using System.Net.Mime;
using JetReportDesigner.Rendering;
using JetReportDesigner.Storage.Assets;

namespace JetReportDesigner.Api.Infrastructure;

/// <summary>
/// Resolves the image references a report can carry into bytes for the renderers:
/// <c>asset:{id}</c> from the asset store, <c>data:</c> URIs inline, and http(s) URLs
/// through the SSRF-guarded <see cref="HttpClient"/> shared with REST data sources.
/// </summary>
public sealed class RenderImageResolver(IAssetRepository assets, HttpClient http) : IRenderImageResolver
{
    private const int MaxBytes = 5 * 1024 * 1024;

    public async Task<ResolvedImage?> ResolveAsync(string source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        if (source.StartsWith("asset:", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(source.AsSpan(6), out var id))
            {
                return null;
            }

            var content = await assets.GetContentAsync(id, cancellationToken);
            return content is null ? null : new ResolvedImage(content.Bytes, content.ContentType);
        }

        if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return ParseDataUri(source);
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return await FetchAsync(uri, cancellationToken);
        }

        return null;
    }

    private static ResolvedImage? ParseDataUri(string source)
    {
        var comma = source.IndexOf(',');
        if (comma < 0)
        {
            return null;
        }

        var meta = source[5..comma];
        if (!meta.Contains(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return null; // only base64 image data URIs are supported
        }

        var contentType = meta.Split(';')[0] is { Length: > 0 } ct ? ct : "image/png";
        try
        {
            var bytes = Convert.FromBase64String(source[(comma + 1)..]);
            return bytes.Length is > 0 and <= MaxBytes ? new ResolvedImage(bytes, contentType) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private async Task<ResolvedImage?> FetchAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? MediaTypeNames.Image.Jpeg;
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return bytes.Length is > 0 and <= MaxBytes ? new ResolvedImage(bytes, contentType) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return null;
        }
    }
}
