using OpenGitBase.Cli.Configuration;

namespace OpenGitBase.Cli.Auth;

public sealed class MacOSKeychainCredentialStore : ICredentialStore
{
    private readonly IHostResolver _hostResolver;

    public MacOSKeychainCredentialStore(IHostResolver hostResolver)
    {
        _hostResolver = hostResolver;
    }

    public void SaveToken(string host, string token)
    {
        var service = GetServiceName(host);
        KeychainProcessRunner.Run(
            $"add-generic-password -a token -s \"{service}\" -w \"{KeychainProcessRunner.EscapeForShell(token)}\" -U");
    }

    public string? GetToken(string host)
    {
        var service = GetServiceName(host);
        try
        {
            return KeychainProcessRunner.Run($"find-generic-password -s \"{service}\" -w", captureOutput: true).Trim();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public void DeleteToken(string host)
    {
        var service = GetServiceName(host);
        try
        {
            KeychainProcessRunner.Run($"delete-generic-password -s \"{service}\"");
        }
        catch (InvalidOperationException)
        {
            // Entry may already be absent.
        }
    }

    public bool HasToken(string host) => !string.IsNullOrWhiteSpace(GetToken(host));

    private string GetServiceName(string host) =>
        $"opengitbase-cli/{_hostResolver.NormalizeHost(host)}";
}
