namespace OpenGitBase.E2E.Core.Fixtures;

public sealed record PatSeed(string Token, string RemoteUrl);

public sealed class PatFixture
{
    private readonly IOperationTranscript _transcript;
    private readonly BaselineNormalizer _normalizer;

    public PatFixture(IOperationTranscript transcript, BaselineNormalizer normalizer)
    {
        _transcript = transcript;
        _normalizer = normalizer;
    }

    public async Task<PatSeed> CreateWritePatAsync(
        AuthenticatedClient user,
        string ownerUsername,
        string repoSlug,
        CancellationToken cancellationToken = default)
    {
        _transcript.Describe("Create write-scoped PAT");
        var response = await user.Client.PostAsync(
            "/git-access-token",
            new
            {
                name = "e2e-write",
                scope = "write",
            },
            cancellationToken).ConfigureAwait(false);
        var token = E2eScenarioHelpers.ParsePatToken(response);
        _normalizer.RegisterToken("WRITE_PAT", token);
        return new PatSeed(token, BuildRemoteUrl(ownerUsername, repoSlug, token));
    }

    public async Task<PatSeed> CreateReadPatAsync(
        AuthenticatedClient user,
        string ownerUsername,
        string repoSlug,
        CancellationToken cancellationToken = default)
    {
        _transcript.Describe("Create read-scoped PAT");
        var response = await user.Client.PostAsync(
            "/git-access-token",
            new
            {
                name = "e2e-read",
                scope = "read",
            },
            cancellationToken).ConfigureAwait(false);
        var token = E2eScenarioHelpers.ParsePatToken(response);
        return new PatSeed(token, BuildRemoteUrl(ownerUsername, repoSlug, token));
    }

    public string BuildRemoteUrl(string ownerUsername, string repoSlug, string pat) =>
        E2eEnvironment.BuildPatRemoteUrl(ownerUsername, repoSlug, pat);
}
