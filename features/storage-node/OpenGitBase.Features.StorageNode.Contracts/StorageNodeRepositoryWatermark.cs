namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class StorageNodeRepositoryWatermark
{
    public Guid RepositoryId { get; set; }

    public long AppliedWatermark { get; set; }
}
