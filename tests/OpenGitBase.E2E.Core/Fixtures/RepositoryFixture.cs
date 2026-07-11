namespace OpenGitBase.E2E.Core.Fixtures;

public sealed record RepositorySeed(
    string Slug,
    string RepositoryId,
    bool IsPrivate,
    string OwnerUsername);

public sealed class RepositoryFixture
{
    private readonly IOperationTranscript _transcript;
    private readonly BaselineNormalizer _normalizer;

    public RepositoryFixture(IOperationTranscript transcript, BaselineNormalizer normalizer)
    {
        _transcript = transcript;
        _normalizer = normalizer;
    }

    public async Task<RepositorySeed> CreateAsync(
        AuthenticatedClient owner,
        string slug,
        string displayName,
        bool isPrivate,
        CancellationToken cancellationToken = default)
    {
        _transcript.Describe(isPrivate ? $"Create private repository {slug}" : $"Create public repository {slug}");
        var response = await owner.Client.PostAsync(
            $"/repository/{slug}",
            new { repositoryName = displayName, isPrivate },
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is not 201 and not 200)
        {
            throw new InvalidOperationException($"Create repository failed: {response.StatusCode} {response.Body}");
        }

        var repositoryId = E2eScenarioHelpers.ParseRepositoryId(response);
        return new RepositorySeed(slug, repositoryId, isPrivate, owner.Username);
    }

    public Task AddMemberAsync(
        AuthenticatedClient owner,
        string repositoryId,
        string userId,
        int role,
        CancellationToken cancellationToken = default)
    {
        _transcript.Describe("Add repository member");
        return owner.Client.PostAsync(
            "/repository-member",
            new
            {
                modelToCreate = new
                {
                    repositoryId = new { value = repositoryId },
                    userId = new { value = userId },
                    role,
                },
            },
            cancellationToken);
    }
}
