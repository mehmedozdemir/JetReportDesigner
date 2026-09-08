using JetReportDesigner.Storage.Connections;
using Microsoft.AspNetCore.DataProtection;

namespace JetReportDesigner.Api.Infrastructure;

/// <summary>Encrypts registered connection strings with ASP.NET Data Protection.</summary>
internal sealed class DataProtectionSecretProtector : IConnectionSecretProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("JetReportDesigner.Connections.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
