using OpenGitBase.Cli.Auth;
using OpenGitBase.Cli.Configuration;

namespace OpenGitBase.Cli.Tests;

public sealed class CredentialStoreFactoryTests
{
    [Fact]
    public void CreateDefault_returns_platform_store()
    {
        var store = CredentialStoreFactory.CreateDefault(new HostResolver());
        if (OperatingSystem.IsMacOS())
        {
            Assert.IsType<MacOSKeychainCredentialStore>(store);
        }
        else
        {
            Assert.IsType<FileCredentialStore>(store);
        }
    }
}
