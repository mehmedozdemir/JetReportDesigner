using Microsoft.AspNetCore.Identity;

namespace JetReportDesigner.Storage.Entities;

/// <summary>The two roles a user can hold: <see cref="Designer"/> can create/edit/delete
/// reports, connections and assets; <see cref="Viewer"/> can only list and render them.</summary>
public sealed class AppRole : IdentityRole<Guid>
{
    public const string Designer = "Designer";
    public const string Viewer = "Viewer";
}
