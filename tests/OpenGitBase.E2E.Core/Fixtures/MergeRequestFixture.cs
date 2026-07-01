namespace OpenGitBase.E2E.Core.Fixtures;

public sealed record MergeRequestReadySeed(
    AuthenticatedClient Owner,
    AuthenticatedClient Writer,
    RepositorySeed Repository,
    string FeatureBranch,
    string WorkDir,
    string MergeRequestBase);

public sealed class MergeRequestFixture
{
    private readonly IOperationTranscript _transcript;
    private readonly RepositoryFixture _repositories;
    private readonly PatFixture _pats;
    private readonly GitOperations _git;

    public MergeRequestFixture(IOperationTranscript transcript, BaselineNormalizer normalizer)
    {
        _transcript = transcript;
        _repositories = new RepositoryFixture(transcript, normalizer);
        _pats = new PatFixture(transcript, normalizer);
        _git = new GitOperations(transcript);
    }

    public async Task<MergeRequestReadySeed> SeedMrReadyAsync(
        AuthenticatedClient owner,
        AuthenticatedClient writer,
        string repoSlug,
        string workRoot,
        string featureBranch = "feature/mr-e2e",
        CancellationToken cancellationToken = default)
    {
        _transcript.Describe("Seed merge-request-ready repository with main and feature branches");
        var repository = await _repositories.CreateAsync(
            owner,
            repoSlug,
            "Merge Request E2E",
            isPrivate: false,
            cancellationToken).ConfigureAwait(false);
        await _repositories.AddMemberAsync(owner, repository.RepositoryId, writer.UserId, role: 2, cancellationToken)
            .ConfigureAwait(false);
        await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);

        var writePat = await _pats.CreateWritePatAsync(owner, owner.Username, repoSlug, cancellationToken)
            .ConfigureAwait(false);
        var workDir = Path.Combine(workRoot, "work");
        await _git.InitRepositoryAsync(workDir, "main", owner.Username, $"{owner.Username}@example.com")
            .ConfigureAwait(false);
        await _git.CommitFileAsync(workDir, "README.md", "initial\n", "initial commit").ConfigureAwait(false);
        await _git.AddRemoteAsync(workDir, "origin", writePat.RemoteUrl).ConfigureAwait(false);
        await _git.PushAsync(workDir, "origin", "main").ConfigureAwait(false);
        await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);
        await _git.CheckoutBranchAsync(workDir, featureBranch, create: true).ConfigureAwait(false);
        await _git.CommitFileAsync(workDir, "README.md", "change\n", "feature commit", append: true)
            .ConfigureAwait(false);
        await _git.PushAsync(workDir, "origin", featureBranch).ConfigureAwait(false);

        var mergeRequestBase = $"/repository/by-slug/{owner.Username}/{repoSlug}/merge-requests";
        return new MergeRequestReadySeed(owner, writer, repository, featureBranch, workDir, mergeRequestBase);
    }
}
