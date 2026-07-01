namespace OpenGitBase.E2E.Core.Fixtures;

public sealed record OrganizationSeed(string Id, string Slug, string Name);

public sealed class OrganizationFixture
{
    private readonly IOperationTranscript _transcript;

    public OrganizationFixture(IOperationTranscript transcript)
    {
        _transcript = transcript;
    }

    public async Task<OrganizationSeed> CreateAsync(
        AuthenticatedClient owner,
        string slug,
        string name,
        CancellationToken cancellationToken = default)
    {
        _transcript.Describe($"Create organization {slug}");
        var response = await owner.Client.PostAsync(
            "/organization",
            new
            {
                modelToCreate = new
                {
                    name,
                    slug,
                },
            },
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is not 201 and not 200)
        {
            throw new InvalidOperationException($"Create organization failed: {response.StatusCode} {response.Body}");
        }

        var organizationId = E2eScenarioHelpers.ParseOrganizationId(response);
        return new OrganizationSeed(organizationId, slug, name);
    }

    public async Task AddMemberAsync(
        AuthenticatedClient owner,
        string organizationId,
        string memberUsername,
        CancellationToken cancellationToken = default)
    {
        _transcript.Describe($"Add organization member {memberUsername}");
        await owner.Client.PostAsync(
            $"/organization/{organizationId}/members",
            new { identifier = memberUsername },
            cancellationToken).ConfigureAwait(false);
    }
}
