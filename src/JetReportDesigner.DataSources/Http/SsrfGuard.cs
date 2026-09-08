using System.Net;
using System.Net.Sockets;

namespace JetReportDesigner.DataSources.Http;

/// <summary>Thrown when a REST data source URL is rejected by the SSRF guard.</summary>
public sealed class SsrfBlockedException(string message) : Exception(message);

/// <summary>
/// Blocks server-side request forgery: only http/https, and the address a request
/// actually connects to must be a routable public unicast address (not loopback,
/// private, link-local, unique-local, or multicast). An optional host allow-list
/// tightens this further. Enforced at connect time to defeat DNS rebinding.
/// </summary>
public sealed class SsrfGuard(IReadOnlyCollection<string>? allowedHosts = null)
{
    private readonly HashSet<string> _allowedHosts =
        new(allowedHosts ?? [], StringComparer.OrdinalIgnoreCase);

    public void ValidateUrl(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new SsrfBlockedException($"Only http and https URLs are allowed (got '{uri.Scheme}').");
        }

        if (_allowedHosts.Count > 0 && !_allowedHosts.Contains(uri.Host))
        {
            throw new SsrfBlockedException($"Host '{uri.Host}' is not in DataSources:Rest:AllowedHosts.");
        }

        // Reject an IP literal in a blocked range up front (defence in depth).
        if (IPAddress.TryParse(uri.Host, out var literal) && !IsPublic(literal))
        {
            throw new SsrfBlockedException($"URL host '{uri.Host}' resolves to a non-routable address.");
        }
    }

    /// <summary>Call from <c>SocketsHttpHandler.ConnectCallback</c> with the resolved endpoint.</summary>
    public void ValidateResolvedEndpoint(string host, IPAddress address)
    {
        if (!IsPublic(address))
        {
            throw new SsrfBlockedException(
                $"Blocked connection to '{host}' ({address}) — non-routable address.");
        }
    }

    private static bool IsPublic(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.IsIPv6Multicast)
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b switch
            {
                [10, ..] => false,                                   // 10/8
                [127, ..] => false,                                  // loopback
                [169, 254, ..] => false,                             // link-local (incl. 169.254.169.254)
                [172, >= 16 and <= 31, ..] => false,                 // 172.16/12
                [192, 168, ..] => false,                             // 192.168/16
                [100, >= 64 and <= 127, ..] => false,                // CGNAT 100.64/10
                [0, ..] or [>= 224, ..] => false,                    // "this network" / multicast / reserved
                _ => true,
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
            {
                return false;
            }

            var b = address.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC)
            {
                return false; // fc00::/7 unique local
            }

            return b[0] != 0xFF; // multicast
        }

        return false;
    }
}
