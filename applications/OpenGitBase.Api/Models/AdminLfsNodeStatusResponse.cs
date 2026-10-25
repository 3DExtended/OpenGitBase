namespace OpenGitBase.Api.Models;

public sealed class AdminLfsNodeStatusResponse
{
    public Guid StorageNodeId { get; init; }

    public string NodeId { get; init; } = string.Empty;

    /// <summary><c>Plaintext</c> or <c>Encrypted</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    public bool IsPrimary { get; init; }

    public int Copies { get; init; }

    public int Missing { get; init; }
}
