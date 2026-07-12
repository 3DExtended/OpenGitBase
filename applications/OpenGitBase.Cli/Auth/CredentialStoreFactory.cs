using OpenGitBase.Cli.Configuration;

namespace OpenGitBase.Cli.Auth;

public static class CredentialStoreFactory
{
    public static ICredentialStore CreateDefault(IHostResolver hostResolver) =>
        OperatingSystem.IsMacOS()
            ? new MacOSKeychainCredentialStore(hostResolver)
            : new FileCredentialStore(hostResolver);
}
