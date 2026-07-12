using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace OpenGitBase.Cli.Configuration;

public sealed class FileConfigStore : IConfigStore
{
    private readonly string _configPath;

    public FileConfigStore(string? configPath = null)
    {
        _configPath = configPath ?? GetDefaultConfigPath();
    }

    public string ConfigPath => _configPath;

    public static string GetDefaultConfigPath()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var baseDir = !string.IsNullOrWhiteSpace(configHome)
            ? configHome
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

        return Path.Combine(baseDir, "ogb", "hosts.yml");
    }

    public OgbConfigFile Load()
    {
        if (!File.Exists(_configPath))
        {
            return new OgbConfigFile();
        }

        var yaml = File.ReadAllText(_configPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<OgbConfigFile>(yaml) ?? new OgbConfigFile();
    }

    public void Save(OgbConfigFile config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var yaml = serializer.Serialize(config);
        File.WriteAllText(_configPath, yaml);

        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(_configPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public void Clear()
    {
        if (File.Exists(_configPath))
        {
            File.Delete(_configPath);
        }
    }
}
