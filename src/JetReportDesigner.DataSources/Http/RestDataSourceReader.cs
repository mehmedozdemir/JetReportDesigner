using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources.Json;

namespace JetReportDesigner.DataSources.Http;

/// <summary>
/// Reads a REST data source: a GET request whose URL, query string and headers may
/// contain <c>{param:name}</c> tokens, returning JSON that is flattened to rows via
/// <see cref="JsonRows"/>. All requests pass the <see cref="SsrfGuard"/>.
/// </summary>
public sealed class RestDataSourceReader(HttpClient httpClient, SsrfGuard guard) : IDataSourceReader
{
    public DataSourceKind Kind => DataSourceKind.Rest;

    public async Task<ResolvedDataSet> ReadAsync(
        DataSourceDefinition definition,
        DataSourceReadContext context,
        CancellationToken cancellationToken)
    {
        var parameters = context.Parameters;
        var config = definition.Rest;
        if (config is null || string.IsNullOrWhiteSpace(config.Url))
        {
            return ResolvedDataSet.Empty;
        }

        if (!string.Equals(config.Method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only GET is supported for REST data sources in this version.");
        }

        var uri = BuildUri(config, parameters);
        guard.ValidateUrl(uri);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        foreach (var (name, value) in config.Headers)
        {
            request.Headers.TryAddWithoutValidation(name, ParameterInterpolation.Apply(value, parameters));
        }

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonRows.Parse(body, config.ResultPath);
    }

    private static Uri BuildUri(RestSourceConfig config, IReadOnlyDictionary<string, object?> parameters)
    {
        var resolvedUrl = ParameterInterpolation.Apply(config.Url, parameters);
        var builder = new UriBuilder(resolvedUrl);

        if (config.Query.Count > 0)
        {
            var extra = string.Join(
                '&',
                config.Query.Select(kv =>
                    $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(ParameterInterpolation.Apply(kv.Value, parameters))}"));

            builder.Query = string.IsNullOrEmpty(builder.Query)
                ? extra
                : $"{builder.Query.TrimStart('?')}&{extra}";
        }

        return builder.Uri;
    }
}
