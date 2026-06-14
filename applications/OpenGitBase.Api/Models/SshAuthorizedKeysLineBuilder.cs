namespace OpenGitBase.Api.Models;

public static class SshAuthorizedKeysLineBuilder
{
    public static string Build(string fingerprint, string publicSshKey) =>
        $"environment=\"SSH_KEY_FINGERPRINT={fingerprint}\" environment=\"SSH_PUBLIC_KEY={publicSshKey}\" {publicSshKey}";
}
