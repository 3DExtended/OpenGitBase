namespace OpenGitBase.Api.Models;

public sealed class RepositoryWatermarkReport
{
    public Guid RepositoryId { get; init; }

    public long AppliedWatermark { get; init; }
}
