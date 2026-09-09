using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Storage.Assets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace JetReportDesigner.Api.Controllers;

[ApiController]
[Route("api/assets")]
public sealed class AssetsController(IAssetRepository repository) : ControllerBase
{
    private const long MaxUploadBytes = 5 * 1024 * 1024;

    [HttpGet]
    [Produces("application/json")]
    public async Task<ActionResult<IReadOnlyList<AssetResponse>>> List(CancellationToken cancellationToken)
    {
        var items = await repository.ListAsync(cancellationToken);
        return Ok(items.Select(AssetResponse.From).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var content = await repository.GetContentAsync(id, cancellationToken);
        if (content is null)
        {
            return NotFound();
        }

        var etag = $"\"{content.Sha256}\"";
        if (Request.Headers.IfNoneMatch.Any(v => v == etag))
        {
            Response.Headers.ETag = etag;
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = etag;
        Response.Headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        return File(content.Bytes, content.ContentType);
    }

    [HttpPost]
    [Produces("application/json")]
    [RequestSizeLimit(MaxUploadBytes + 64 * 1024)]
    public async Task<ActionResult<AssetResponse>> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Problem("No file was uploaded.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > MaxUploadBytes)
        {
            return Problem(
                $"The image is larger than the {MaxUploadBytes / (1024 * 1024)} MB limit.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        var contentType = ImageSniffer.Detect(bytes);
        if (contentType is null)
        {
            return Problem(
                "Unsupported image format. Use PNG, JPEG, GIF, WebP or BMP.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var name = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "image";
        }
        else if (name.Length > 260)
        {
            name = name[^260..];
        }

        var saved = await repository.AddAsync(bytes, contentType, name, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = saved.Id }, AssetResponse.From(saved));
    }
}
