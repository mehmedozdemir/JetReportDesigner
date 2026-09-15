using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;

namespace JetReportDesigner.Api.Localization;

/// <summary>
/// The API's own user-facing messages, per language. JSON rather than .resx so they can be
/// edited the same way the web app's locale files are — that was the point of choosing JSON on
/// the front end, and splitting the two conventions would just mean two things to learn.
///
/// The language comes from the request's Accept-Language (see Program.cs), which the web app
/// sets from the language chosen in Settings rather than leaving it to the browser's own
/// preference — otherwise a Turkish UI would still be getting English errors.
/// </summary>
public interface IApiStrings
{
    /// <summary>The message for <paramref name="key"/> in the request's language, falling back to
    /// English, then to the key itself so a missing entry is visible rather than blank.</summary>
    string this[string key] { get; }

    string Format(string key, params object[] args);
}

internal sealed class ApiStrings : IApiStrings
{
    public const string DefaultCulture = "en";

    private static readonly FrozenDictionary<string, FrozenDictionary<string, string>> Catalogues = Load();

    public string this[string key] => Lookup(key) ?? key;

    public string Format(string key, params object[] args)
    {
        var template = Lookup(key);
        return template is null ? key : string.Format(CultureInfo.CurrentUICulture, template, args);
    }

    private static string? Lookup(string key)
    {
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (Catalogues.TryGetValue(language, out var catalogue) && catalogue.TryGetValue(key, out var text))
        {
            return text;
        }

        return Catalogues.TryGetValue(DefaultCulture, out var fallback) && fallback.TryGetValue(key, out var english)
            ? english
            : null;
    }

    private static FrozenDictionary<string, FrozenDictionary<string, string>> Load()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Localization");
        var catalogues = new Dictionary<string, FrozenDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.Exists(directory) ? Directory.GetFiles(directory, "strings.*.json") : [])
        {
            // strings.tr.json -> tr
            var language = Path.GetFileNameWithoutExtension(file).Split('.').Last();
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
            if (entries is not null)
            {
                catalogues[language] = entries.ToFrozenDictionary(StringComparer.Ordinal);
            }
        }

        return catalogues.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
