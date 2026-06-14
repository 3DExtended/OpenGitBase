namespace OpenGitBase.Api.Models;

public sealed class SshAuthenticationResponse
{
    public required string Fingerprint { get; init; }
    public required string PublicSshKey { get; init; }
    public required string AuthorizedKeysLine { get; init; }
}
