namespace OpenGitBase.Api.Models;

public sealed class CreatePublicGitSshKeyModelRequest
{
    public string Name { get; init; } = string.Empty;

    public string PublicSSHKey { get; init; } = string.Empty;

    public string? Fingerprint { get; init; }
}
