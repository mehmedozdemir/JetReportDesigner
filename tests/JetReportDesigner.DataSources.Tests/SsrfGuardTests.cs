using System.Net;
using JetReportDesigner.DataSources.Http;

namespace JetReportDesigner.DataSources.Tests;

public class SsrfGuardTests
{
    private static readonly SsrfGuard Guard = new();

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///etc/passwd")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://127.0.0.1:8080/admin")]
    [InlineData("http://10.1.2.3/internal")]
    [InlineData("http://192.168.0.10/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://172.16.5.4/")]
    public void ValidateUrl_Rejects_NonHttp_And_Internal_Literals(string url)
    {
        Assert.Throws<SsrfBlockedException>(() => Guard.ValidateUrl(new Uri(url)));
    }

    [Theory]
    [InlineData("https://api.example.com/orders")]
    [InlineData("http://data.internal.example/reports?from=2026-01-01")]
    public void ValidateUrl_Allows_Public_Hostnames(string url)
    {
        Guard.ValidateUrl(new Uri(url)); // does not throw
    }

    [Fact]
    public void AllowList_Restricts_Hosts()
    {
        var restricted = new SsrfGuard(["api.example.com"]);

        restricted.ValidateUrl(new Uri("https://api.example.com/x"));
        Assert.Throws<SsrfBlockedException>(() => restricted.ValidateUrl(new Uri("https://other.example.com/x")));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("192.168.1.1")]
    [InlineData("::1")]
    [InlineData("fd00::1")]
    public void ValidateResolvedEndpoint_Rejects_NonRoutable(string ip)
    {
        Assert.Throws<SsrfBlockedException>(() => Guard.ValidateResolvedEndpoint("host", IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]
    public void ValidateResolvedEndpoint_Allows_PublicUnicast(string ip)
    {
        Guard.ValidateResolvedEndpoint("host", IPAddress.Parse(ip)); // does not throw
    }
}
