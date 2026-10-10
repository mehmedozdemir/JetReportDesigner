using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RaporOnizleyici.Services;

/// <summary>One saved target system: a name (UAT, Prod…), its address and the API key for it.</summary>
public sealed class SystemEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    /// <summary>Plain text in memory only; on disk it is protected with the current Windows user's key (DPAPI).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string ApiKey { get; set; } = string.Empty;

    public string ProtectedApiKey { get; set; } = string.Empty;

    public override string ToString() => Name;
}

/// <summary>Keeps the list of systems in %LOCALAPPDATA%\RaporOnizleyici\systems.json. API keys are DPAPI-protected, never plain text.</summary>
public sealed class EnvironmentStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _path;

    public EnvironmentStore(string? directory = null)
    {
        var dir = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RaporOnizleyici");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "systems.json");
    }

    public List<SystemEntry> Systems { get; private set; } = [];

    public Guid? LastSelected { get; set; }

    public void Load()
    {
        Systems = [];
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            var file = JsonSerializer.Deserialize<StoreFile>(File.ReadAllText(_path, Encoding.UTF8), Json);
            if (file is null)
            {
                return;
            }

            LastSelected = file.LastSelected;
            foreach (var s in file.Systems)
            {
                s.ApiKey = Unprotect(s.ProtectedApiKey);
                Systems.Add(s);
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // A damaged file must not stop the app: start empty and let the user add the systems again.
            Systems = [];
        }
    }

    public void Save()
    {
        foreach (var s in Systems)
        {
            s.ProtectedApiKey = Protect(s.ApiKey);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(new StoreFile { Systems = Systems, LastSelected = LastSelected }, Json), Encoding.UTF8);
    }

    private static string Protect(string plain) =>
        string.IsNullOrEmpty(plain)
            ? string.Empty
            : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser));

    private static string Unprotect(string protectedText)
    {
        if (string.IsNullOrEmpty(protectedText))
        {
            return string.Empty;
        }

        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedText), null, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return string.Empty; // another Windows user / machine: the key has to be entered again
        }
    }

    private sealed class StoreFile
    {
        public List<SystemEntry> Systems { get; set; } = [];

        public Guid? LastSelected { get; set; }
    }
}
