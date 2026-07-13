using OpenGitBase.Cli.Auth;
using OpenGitBase.Cli.Configuration;

namespace OpenGitBase.Cli.Tests;

public sealed class FileCredentialStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _credentialPath;
    private readonly HostResolver _hostResolver = new();

    public FileCredentialStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ogb-cred-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _credentialPath = Path.Combine(_tempDir, "credentials.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Save_get_delete_round_trip()
    {
        var store = new FileCredentialStore(_hostResolver, _credentialPath);
        store.SaveToken("https://www.opengitbase.com/", "jwt-one");
        Assert.Equal("jwt-one", store.GetToken("https://www.opengitbase.com"));
        Assert.True(store.HasToken("https://www.opengitbase.com/"));

        store.DeleteToken("https://www.opengitbase.com");
        Assert.False(store.HasToken("https://www.opengitbase.com"));
    }

    [Fact]
    public void Save_sets_restrictive_unix_permissions()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var store = new FileCredentialStore(_hostResolver, _credentialPath);
        store.SaveToken("https://localhost:8089", "jwt");

        var mode = File.GetUnixFileMode(_credentialPath);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }
}
