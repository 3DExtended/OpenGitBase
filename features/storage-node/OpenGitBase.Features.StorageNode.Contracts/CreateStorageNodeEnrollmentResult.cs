namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class CreateStorageNodeEnrollmentResult
{
    public Guid EnrollmentId { get; init; }

    public string NodeId { get; init; } = string.Empty;

    public string EnrollmentToken { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; init; }
}
