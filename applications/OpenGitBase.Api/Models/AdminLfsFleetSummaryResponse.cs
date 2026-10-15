namespace OpenGitBase.Api.Models;

public sealed class AdminLfsFleetSummaryResponse
{
    public int ObjectCount { get; init; }

    public long TotalBytes { get; init; }

    public int MissingCopies { get; init; }

    public int RepositoriesWithMissingCopies { get; init; }
}
