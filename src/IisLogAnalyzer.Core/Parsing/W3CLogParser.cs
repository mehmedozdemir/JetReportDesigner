using System.Globalization;
using IisLogAnalyzer.Core.Models;

namespace IisLogAnalyzer.Core.Parsing;

public sealed class LogParseException(string message) : Exception(message);

public sealed record ParseStats(int LinesRead, int EntriesParsed, int LinesSkipped);

/// <summary>
/// Streaming parser for the IIS W3C Extended Log File Format.
/// Reads line-by-line so memory use stays proportional to a single line,
/// not the whole file; field order is taken from each file's own #Fields
/// directive since IIS installations can enable/disable columns.
/// </summary>
public sealed class W3CLogParser
{
    public event Action<string>? OnWarning;

    public IEnumerable<LogEntry> ParseFiles(IEnumerable<string> filePaths)
    {
        foreach (var path in filePaths)
        {
            foreach (var entry in ParseFile(path))
                yield return entry;
        }
    }

    public IEnumerable<LogEntry> ParseFile(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        FieldMap? fieldMap = null;
        string? line;
        int lineNumber = 0;

        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;

            if (line.Length == 0)
                continue;

            if (line[0] == '#')
            {
                if (line.StartsWith("#Fields:", StringComparison.Ordinal))
                    fieldMap = FieldMap.Parse(line);
                continue;
            }

            if (fieldMap is null)
            {
                OnWarning?.Invoke($"{filePath}:{lineNumber} - #Fields yönergesinden önce veri satırı bulundu, atlandı.");
                continue;
            }

            LogEntry? entry = null;
            try
            {
                entry = ParseLine(line, fieldMap);
            }
            catch (Exception ex)
            {
                OnWarning?.Invoke($"{filePath}:{lineNumber} - satır ayrıştırılamadı: {ex.Message}");
            }

            if (entry is not null)
                yield return entry;
        }
    }

    private static LogEntry? ParseLine(string line, FieldMap map)
    {
        Span<Range> ranges = stackalloc Range[map.FieldCount + 8];
        var span = line.AsSpan();
        int count = span.Split(ranges, ' ', StringSplitOptions.RemoveEmptyEntries);

        if (count == 0)
            return null;

        // Materialize fields into a plain string array so the field-lookup helper
        // below doesn't need to capture the ref-struct span/ranges in a closure.
        var fields = new string?[count];
        for (int i = 0; i < count; i++)
        {
            var value = span[ranges[i]];
            fields[i] = value.Length == 1 && value[0] == '-' ? null : value.ToString();
        }

        string? Get(int index) => index >= 0 && index < fields.Length ? fields[index] : null;

        var dateStr = Get(map.DateIndex);
        var timeStr = Get(map.TimeIndex);
        var timestamp = CombineDateTime(dateStr, timeStr);

        var method = Get(map.MethodIndex) ?? "UNKNOWN";
        var uriStem = Get(map.UriStemIndex) ?? "/";
        var statusStr = Get(map.StatusIndex);
        var timeTakenStr = Get(map.TimeTakenIndex);

        if (!int.TryParse(statusStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var status))
            status = 0;

        if (!long.TryParse(timeTakenStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeTaken))
            timeTaken = 0;

        _ = int.TryParse(Get(map.SubStatusIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out var subStatus);
        _ = int.TryParse(Get(map.Win32StatusIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out var win32);

        int? port = null;
        if (int.TryParse(Get(map.PortIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort))
            port = parsedPort;

        return new LogEntry
        {
            Timestamp = timestamp,
            ServerIp = Get(map.ServerIpIndex),
            Method = method,
            UriStem = uriStem,
            UriQuery = Get(map.UriQueryIndex),
            Port = port,
            Username = Get(map.UsernameIndex),
            ClientIp = Get(map.ClientIpIndex),
            UserAgent = Get(map.UserAgentIndex),
            Referer = Get(map.RefererIndex),
            StatusCode = status,
            SubStatusCode = subStatus,
            Win32Status = win32,
            TimeTakenMs = timeTaken,
        };
    }

    private static DateTime CombineDateTime(string? dateStr, string? timeStr)
    {
        if (dateStr is not null && timeStr is not null &&
            DateTime.TryParse($"{dateStr} {timeStr}", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var combined))
        {
            return combined;
        }

        return DateTime.MinValue;
    }

    private sealed class FieldMap
    {
        public int FieldCount;
        public int DateIndex = -1;
        public int TimeIndex = -1;
        public int ServerIpIndex = -1;
        public int MethodIndex = -1;
        public int UriStemIndex = -1;
        public int UriQueryIndex = -1;
        public int PortIndex = -1;
        public int UsernameIndex = -1;
        public int ClientIpIndex = -1;
        public int UserAgentIndex = -1;
        public int RefererIndex = -1;
        public int StatusIndex = -1;
        public int SubStatusIndex = -1;
        public int Win32StatusIndex = -1;
        public int TimeTakenIndex = -1;

        public static FieldMap Parse(string fieldsLine)
        {
            var namesPart = fieldsLine["#Fields:".Length..].Trim();
            var names = namesPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var map = new FieldMap { FieldCount = names.Length };

            for (int i = 0; i < names.Length; i++)
            {
                switch (names[i])
                {
                    case "date": map.DateIndex = i; break;
                    case "time": map.TimeIndex = i; break;
                    case "s-ip": map.ServerIpIndex = i; break;
                    case "cs-method": map.MethodIndex = i; break;
                    case "cs-uri-stem": map.UriStemIndex = i; break;
                    case "cs-uri-query": map.UriQueryIndex = i; break;
                    case "s-port": map.PortIndex = i; break;
                    case "cs-username": map.UsernameIndex = i; break;
                    case "c-ip": map.ClientIpIndex = i; break;
                    case "cs(User-Agent)": map.UserAgentIndex = i; break;
                    case "cs(Referer)": map.RefererIndex = i; break;
                    case "sc-status": map.StatusIndex = i; break;
                    case "sc-substatus": map.SubStatusIndex = i; break;
                    case "sc-win32-status": map.Win32StatusIndex = i; break;
                    case "time-taken": map.TimeTakenIndex = i; break;
                }
            }

            return map;
        }
    }
}
