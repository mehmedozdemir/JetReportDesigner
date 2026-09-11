using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Storage.Folders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

[ApiController]
[Route("api/folders")]
[Produces("application/json")]
public sealed class FoldersController(IFolderRepository folders) : ControllerBase
{
    /// <summary>Every folder in the tenant. The client builds the tree from ParentFolderId.
    /// No folder-level permissions — visibility follows the same role as reports.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FolderResponse>>> List(CancellationToken cancellationToken)
    {
        var list = await folders.ListAsync(cancellationToken);
        return Ok(list.Select(FolderResponse.From).ToList());
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<ActionResult<FolderResponse>> Create([FromBody] CreateFolderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationProblem("Name is required.");
        }

        var created = await folders.CreateAsync(request.Name.Trim(), request.ParentFolderId, cancellationToken);
        return created is null
            ? ValidationProblem("parentFolderId does not exist.")
            : Ok(FolderResponse.From(created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<ActionResult<FolderResponse>> Rename(Guid id, [FromBody] RenameFolderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationProblem("Name is required.");
        }

        var renamed = await folders.RenameAsync(id, request.Name.Trim(), cancellationToken);
        return renamed is null ? NotFound() : Ok(FolderResponse.From(renamed));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await folders.DeleteAsync(id, cancellationToken);
        return result switch
        {
            FolderDeleteResult.Deleted => NoContent(),
            FolderDeleteResult.NotFound => NotFound(),
            FolderDeleteResult.NotEmpty => Problem(
                "This folder still has subfolders or reports in it. Move or delete those first.",
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException($"Unhandled {nameof(FolderDeleteResult)}: {result}"),
        };
    }
}
