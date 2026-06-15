namespace OpenGitBase.Api.Models;

public static class SshAuthorizedKeysLineBuilder
{
    // Only fingerprint goes in environment= — OpenSSH rejects values containing spaces
    // (e.g. a full "ssh-ed25519 AAAA... comment" public key).
    public static string Build(string fingerprint, string publicSshKey) =>
        $"environment=\"SSH_KEY_FINGERPRINT={fingerprint}\" {publicSshKey}";
}
