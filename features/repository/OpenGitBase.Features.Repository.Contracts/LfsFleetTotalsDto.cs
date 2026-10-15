namespace OpenGitBase.Features.Repository.Contracts;

public sealed class LfsFleetTotalsDto
{
    public int ObjectCount { get; init; }

    public long TotalBytes { get; init; }

    /// <summary>Copies that the repositories' nodes should hold and do not, summed over every repository.</summary>
    public int MissingCopies { get; init; }

    public int RepositoriesWithMissingCopies { get; init; }
}
