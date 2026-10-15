namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentTreeResult
{
    public StorageContentTreePayload? Payload { get; init; }

    // The node holds the revision but no directory at this path. A listing that failed for any
    // other reason, including a revision the node lacks, leaves this false and Payload null.
    public bool IsMissing { get; init; }
}
