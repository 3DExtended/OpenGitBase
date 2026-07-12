using OpenGitBase.Cli.Configuration;

namespace OpenGitBase.Cli.Tests;

public sealed class FileConfigStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public FileConfigStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ogb-cli-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "hosts.yml");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_returns_empty_when_missing()
    {
        var store = new FileConfigStore(_configPath);
        var config = store.Load();

        Assert.Null(config.ActiveHost);
    }

    [Fact]
    public void Save_and_load_round_trip()
    {
        var store = new FileConfigStore(_configPath);
        store.Save(new OgbConfigFile { ActiveHost = HostDefaults.ProductionHost, LoggedInUsername = "alice" });

        var loaded = store.Load();
        Assert.Equal(HostDefaults.ProductionHost, loaded.ActiveHost);
        Assert.Equal("alice", loaded.LoggedInUsername);
    }

    [Fact]
    public void Save_sets_restrictive_unix_permissions()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var store = new FileConfigStore(_configPath);
        store.Save(new OgbConfigFile { ActiveHost = HostDefaults.ProductionHost });

        var mode = File.GetUnixFileMode(_configPath);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }
}
