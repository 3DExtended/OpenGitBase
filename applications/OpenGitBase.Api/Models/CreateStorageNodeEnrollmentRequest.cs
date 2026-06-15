namespace OpenGitBase.Api.Models;

public sealed class CreateStorageNodeEnrollmentRequest
{
    public string NodeId { get; init; } = string.Empty;

    public int ExpiresInHours { get; init; } = 168;
}
