using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

/// <summary>Serves repository files from memory, standing in for the storage content API.</summary>
internal sealed class FakeRepositoryFileReader : IRepositoryFileReader
{
    private readonly Dictionary<string, string> _files;
    private readonly Exception? _failure;

    private FakeRepositoryFileReader(Dictionary<string, string> files, Exception? failure)
    {
        _files = files;
        _failure = failure;
    }

    public List<(Guid RepositoryId, string PhysicalPath, string CommitSha, string Path)> Reads { get; } =
        new();

    public static FakeRepositoryFileReader WithFiles(IReadOnlyDictionary<string, string> files) =>
        new(new Dictionary<string, string>(files, StringComparer.Ordinal), failure: null);

    public static FakeRepositoryFileReader Failing(Exception failure) => new([], failure);

    public Task<string?> ReadTextAsync(
        Guid repositoryId,
        string physicalPath,
        string commitSha,
        string path,
        CancellationToken cancellationToken
    )
    {
        Reads.Add((repositoryId, physicalPath, commitSha, path));
        if (_failure is not null)
        {
            throw _failure;
        }

        return Task.FromResult(_files.TryGetValue(path, out var content) ? content : null);
    }
}
