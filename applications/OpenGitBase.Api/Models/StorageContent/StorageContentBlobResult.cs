namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentBlobResult
{
    public StorageContentBlobPayload? Payload { get; init; }

    // The node holds the revision but no file at this path. A read that failed for any other
    // reason, including a revision the node lacks, leaves this false and Payload null.
    public bool IsMissing { get; init; }
}
