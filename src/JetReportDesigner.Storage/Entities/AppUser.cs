using Microsoft.AspNetCore.Identity;

namespace JetReportDesigner.Storage.Entities;

/// <summary>An application user. Authentication is username/password (via Identity's
/// password hasher) exchanged for a JWT — no cookie sign-in is used.</summary>
public sealed class AppUser : IdentityUser<Guid>;
