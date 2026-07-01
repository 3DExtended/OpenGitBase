namespace OpenGitBase.E2E.Core.Fixtures;

/// <summary>
/// Documented on-disk layout for programmatic git testdata repos (not committed as baselines).
/// </summary>
public static class GitTestDataLayout
{
    public const string ReadmePath = "README.md";

    public const string ReadmeContent = "browse-fixture\n";

    public const string NestedPath = "src/foo/bar.txt";

    public const string NestedContent = "nested-fixture-content\n";

    public const string SvgPath = "assets/logo.svg";

    public const string SvgContent = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16"><circle cx="8" cy="8" r="6"/></svg>""";

    public const string LargeBlobPath = "data/large.bin";

    public const int LargeBlobSizeBytes = 1_048_577;

    public const string AnchorPath = "docs/anchors.md";

    public const string AnchorMarker = "<!-- anchor:discussion-target -->";

    public const string AnchorBody = $"{AnchorMarker}\nline 10: target function\n";
}

public sealed record GitTestDataRepo(
    AuthenticatedClient Owner,
    string Slug,
    string OwnerUsername,
    string WorkDir);

/// <summary>
/// Seeds a public repository with a known file tree via HTTPS PAT push.
/// Reuse one instance per test class to avoid duplicate pushes within a class.
/// </summary>
public sealed class GitTestDataFixture
{
    private readonly IOperationTranscript _transcript;
    private readonly BaselineNormalizer _normalizer;
    private readonly RepositoryFixture _repositories;
    private readonly PatFixture _pats;
    private readonly GitOperations _git;
    private GitTestDataRepo? _browseRepo;
    private GitTestDataRepo? _anchorRepo;

    public GitTestDataFixture(IOperationTranscript transcript, BaselineNormalizer normalizer)
    {
        _transcript = transcript;
        _normalizer = normalizer;
        _repositories = new RepositoryFixture(transcript, normalizer);
        _pats = new PatFixture(transcript, normalizer);
        _git = new GitOperations(transcript);
    }

    public async Task<GitTestDataRepo> GetBrowsePublicRepoAsync(
        AuthenticatedClient owner,
        string runSuffix,
        string workRoot,
        CancellationToken cancellationToken = default)
    {
        if (_browseRepo != null)
        {
            return _browseRepo;
        }

        _browseRepo = await SeedFullTreeAsync(owner, $"gitdata-browse-{runSuffix}", workRoot, cancellationToken)
            .ConfigureAwait(false);
        return _browseRepo;
    }

    public async Task<GitTestDataRepo> GetAnchorRepoAsync(
        AuthenticatedClient owner,
        string runSuffix,
        string workRoot,
        CancellationToken cancellationToken = default)
    {
        if (_anchorRepo != null)
        {
            return _anchorRepo;
        }

        _anchorRepo = await SeedFullTreeAsync(owner, $"gitdata-anchor-{runSuffix}", workRoot, cancellationToken)
            .ConfigureAwait(false);
        return _anchorRepo;
    }

    private async Task<GitTestDataRepo> SeedFullTreeAsync(
        AuthenticatedClient owner,
        string slug,
        string workRoot,
        CancellationToken cancellationToken)
    {
        _transcript.Describe("Provision git testdata repository with known file tree");
        await _repositories.CreateAsync(owner, slug, "Git Testdata", isPrivate: false, cancellationToken)
            .ConfigureAwait(false);
        await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);

        var pat = await _pats.CreateWritePatAsync(owner, owner.Username, slug, cancellationToken).ConfigureAwait(false);
        var workDir = Path.Combine(workRoot, slug);
        await _git.InitRepositoryAsync(workDir, "main", owner.Username, $"{owner.Username}@example.com", cancellationToken)
            .ConfigureAwait(false);

        WriteTextFile(workDir, GitTestDataLayout.ReadmePath, GitTestDataLayout.ReadmeContent);
        WriteTextFile(workDir, GitTestDataLayout.NestedPath, GitTestDataLayout.NestedContent);
        WriteTextFile(workDir, GitTestDataLayout.SvgPath, GitTestDataLayout.SvgContent);
        WriteTextFile(workDir, GitTestDataLayout.AnchorPath, GitTestDataLayout.AnchorBody);
        WriteLargeBlob(workDir, GitTestDataLayout.LargeBlobPath, GitTestDataLayout.LargeBlobSizeBytes);

        await _git.CommitPathsAsync(workDir, "seed git testdata tree", cancellationToken).ConfigureAwait(false);
        await _git.AddRemoteAsync(workDir, "origin", pat.RemoteUrl, cancellationToken).ConfigureAwait(false);
        await _git.PushAsync(workDir, "origin", "main", cancellationToken).ConfigureAwait(false);
        await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);

        return new GitTestDataRepo(owner, slug, owner.Username, workDir);
    }

    private static void WriteTextFile(string workDir, string relativePath, string content)
    {
        var fullPath = Path.Combine(workDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private static void WriteLargeBlob(string workDir, string relativePath, int sizeBytes)
    {
        var fullPath = Path.Combine(workDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using var stream = File.Create(fullPath);
        var buffer = new byte[8192];
        var remaining = sizeBytes;
        while (remaining > 0)
        {
            var chunk = Math.Min(buffer.Length, remaining);
            stream.Write(buffer, 0, chunk);
            remaining -= chunk;
        }
    }
}
