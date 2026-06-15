namespace OpenGitBase.Api.Models;

public sealed class CreatePublicGitSshKeyRequest
{
    public CreatePublicGitSshKeyModelRequest ModelToCreate { get; init; } = default!;
}
