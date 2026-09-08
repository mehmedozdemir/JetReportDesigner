using System.Net.Sockets;

namespace JetReportDesigner.DataSources.Http;

public sealed class RestSourceOptions
{
    public const string SectionName = "DataSources:Rest";

    /// <summary>When non-empty, a REST URL's host must be one of these.</summary>
    public string[] AllowedHosts { get; set; } = [];

    public int TimeoutSeconds { get; set; } = 30;

    public long MaxResponseBytes { get; set; } = 10 * 1024 * 1024;
}

/// <summary>Builds an <see cref="HttpClient"/> hardened for fetching external report data.</summary>
public static class RestHttp
{
    public static HttpClient CreateClient(SsrfGuard guard, RestSourceOptions options)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(Math.Min(options.TimeoutSeconds, 15)),
            ConnectCallback = async (context, cancellationToken) =>
            {
                var host = context.DnsEndPoint.Host;
                var addresses = await System.Net.Dns.GetHostAddressesAsync(host, cancellationToken);
                foreach (var address in addresses)
                {
                    guard.ValidateResolvedEndpoint(host, address);
                }

                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
            MaxResponseContentBufferSize = options.MaxResponseBytes,
        };
    }
}
