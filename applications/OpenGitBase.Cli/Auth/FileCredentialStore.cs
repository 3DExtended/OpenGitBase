using System.Text.Json;
using OpenGitBase.Cli.Configuration;

namespace OpenGitBase.Cli.Auth;

public sealed class FileCredentialStore : ICredentialStore
{
    private readonly string _filePath;
    private readonly IHostResolver _hostResolver;

    public FileCredentialStore(IHostResolver hostResolver, string? filePath = null)
    {
        _hostResolver = hostResolver;
        _filePath = filePath ?? GetDefaultPath();
    }

    public string FilePath => _filePath;

    public static string GetDefaultPath()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var baseDir = !string.IsNullOrWhiteSpace(configHome)
            ? configHome
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

        return Path.Combine(baseDir, "ogb", "credentials.json");
    }

    public void SaveToken(string host, string token)
    {
        var normalizedHost = _hostResolver.NormalizeHost(host);
        var entries = LoadEntries();
        entries[normalizedHost] = token;
        SaveEntries(entries);
    }

    public string? GetToken(string host)
    {
        var normalizedHost = _hostResolver.NormalizeHost(host);
        return LoadEntries().TryGetValue(normalizedHost, out var token) ? token : null;
    }

    public void DeleteToken(string host)
    {
        var normalizedHost = _hostResolver.NormalizeHost(host);
        var entries = LoadEntries();
        if (entries.Remove(normalizedHost))
        {
            SaveEntries(entries);
        }
    }

    public bool HasToken(string host) => !string.IsNullOrWhiteSpace(GetToken(host));

    private Dictionary<string, string> LoadEntries()
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private void SaveEntries(Dictionary<string, string> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
