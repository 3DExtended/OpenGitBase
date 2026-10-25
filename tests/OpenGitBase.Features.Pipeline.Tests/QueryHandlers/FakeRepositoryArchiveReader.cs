using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

/// <summary>Serves a fixed workspace archive, standing in for the storage content API.</summary>
internal sealed class FakeRepositoryArchiveReader : IRepositoryArchiveReader
{
    private readonly byte[] _archive;
    private readonly Exception? _failure;

    private FakeRepositoryArchiveReader(byte[] archive, Exception? failure)
    {
        _archive = archive;
        _failure = failure;
    }

    public List<(Guid RepositoryId, string PhysicalPath, string CommitSha, int GitDepth)> Reads { get; } =
        new();

    public static FakeRepositoryArchiveReader WithArchive(byte[] archive) => new(archive, failure: null);

    public static FakeRepositoryArchiveReader Failing(Exception failure) => new([], failure);

    public Task<byte[]> ReadWorkspaceArchiveAsync(
        Guid repositoryId,
        string physicalPath,
        string commitSha,
        int gitDepth,
        CancellationToken cancellationToken
    )
    {
        Reads.Add((repositoryId, physicalPath, commitSha, gitDepth));
        if (_failure is not null)
        {
            throw _failure;
        }

        return Task.FromResult(_archive);
    }
}
