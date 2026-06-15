namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class StorageNodeEnrollmentDto
{
    public Guid Id { get; init; }

    public string NodeId { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? ConsumedAt { get; init; }
}
