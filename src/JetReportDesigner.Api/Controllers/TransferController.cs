using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Api.Transfer;
using JetReportDesigner.Core.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

/// <summary>
/// Move reports between environments as a package (<c>.jrdpkg</c>): export from UAT, preview and import into
/// production. Designers only — importing writes reports, so it needs the same right as editing them.
/// </summary>
[ApiController]
[Route("api/transfer")]
[Authorize(Policy = AuthPolicies.Designer)]
public sealed class TransferController(TransferService transfer) : ControllerBase
{
    /// <summary>What an export of this selection would contain — shown before the file is made.</summary>
    [HttpPost("export/plan")]
    public async Task<ActionResult<ExportPlan>> PlanExport([FromBody] ExportRequest request, CancellationToken ct) =>
        Ok(await transfer.PlanExportAsync(request, ct));

    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] ExportRequest request, CancellationToken ct)
    {
        try
        {
            var package = await transfer.ExportAsync(request, CurrentEmail(), ct);
            return File(package.Content, "application/zip", package.FileName);
        }
        catch (InvalidPackageException ex)
        {
            return Problem(title: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Reads a package and says exactly what importing it would do. Changes nothing.</summary>
    [HttpPost("import/preview")]
    [RequestSizeLimit(PackageIo.MaxPackageBytes + 1_048_576)]
    [RequestFormLimits(MultipartBodyLengthLimit = PackageIo.MaxPackageBytes + 1_048_576)]
    public async Task<ActionResult<ImportPlan>> Preview(IFormFile file, [FromForm] string? options, CancellationToken ct)
    {
        try
        {
            return Ok(await transfer.PreviewAsync(await ReadAsync(file, ct), ParseOptions(options), ct));
        }
        catch (InvalidPackageException ex)
        {
            return Problem(title: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Imports the package, all-or-nothing. The server re-checks everything; it does not trust an earlier preview.</summary>
    [HttpPost("import")]
    [RequestSizeLimit(PackageIo.MaxPackageBytes + 1_048_576)]
    [RequestFormLimits(MultipartBodyLengthLimit = PackageIo.MaxPackageBytes + 1_048_576)]
    public async Task<ActionResult<ImportResult>> Import(IFormFile file, [FromForm] string? options, CancellationToken ct)
    {
        try
        {
            return Ok(await transfer.ApplyAsync(await ReadAsync(file, ct), ParseOptions(options), CurrentEmail(), ct));
        }
        catch (InvalidPackageException ex)
        {
            return Problem(title: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<byte[]> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new InvalidPackageException("No package file was uploaded.");
        }

        if (file.Length > PackageIo.MaxPackageBytes)
        {
            throw new InvalidPackageException("The file is larger than the allowed package size.");
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    private static ImportOptions ParseOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ImportOptions(null, null);
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<ImportOptions>(json, ReportJson.Options) ?? new ImportOptions(null, null);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new InvalidPackageException("The import options are not valid.");
        }
    }

    private string? CurrentEmail() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
}
