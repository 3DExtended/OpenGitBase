using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Organization;

[Collection("Compose")]
[Trait("Category", "Organization")]
[Trait("Tag", "Smoke")]
[Trait("RequiresCompose", "true")]
[E2eTier(5)]
public class OrganizationSmokeTests : E2eTestBase
{
    private readonly OrganizationFixture _organizations;

    public OrganizationSmokeTests()
    {
        _organizations = new OrganizationFixture(Transcript);
    }

    [RequiresComposeFact]
    public async Task CreateOrganizationReturnsCreated()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"org-smoke-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var slug = $"org-smoke-{Context.RunSuffix}";

        Transcript.Describe("Create organization as verified owner");
        var create = await owner.Client.PostAsync("/organization", new
        {
            modelToCreate = new
            {
                name = "Smoke Org",
                slug,
            },
        }).ConfigureAwait(false);
        Assert.Equal(201, create.StatusCode);
        await Baselines.CaptureApiAsync("create-organization", create).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task ListIncludesCreatedOrganization()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"org-list-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var slug = $"org-list-{Context.RunSuffix}";
        await _organizations.CreateAsync(owner, slug, "List Org").ConfigureAwait(false);

        Transcript.Describe("Organization list includes created organization");
        var list = await owner.Client.GetAsync("/organization").ConfigureAwait(false);
        Assert.Equal(200, list.StatusCode);
        Assert.Contains(slug, list.Body, StringComparison.Ordinal);
        await Baselines.CaptureApiAsync("list-organizations", list).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task GetBySlugReturnsOrganization()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"org-slug-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var slug = $"org-slug-{Context.RunSuffix}";
        await _organizations.CreateAsync(owner, slug, "Slug Org").ConfigureAwait(false);

        Transcript.Describe("Get organization by slug succeeds");
        var bySlug = await owner.Client.GetAsync($"/organization/by-slug/{slug}").ConfigureAwait(false);
        Assert.Equal(200, bySlug.StatusCode);
        Assert.Contains(slug, bySlug.Body, StringComparison.Ordinal);
        await Baselines.CaptureApiAsync("get-by-slug", bySlug).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OutsiderCannotListOrganizationMembers()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"org-members-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"org-members-outsider-{Context.RunSuffix}").ConfigureAwait(false);
        var org = await _organizations.CreateAsync(owner, $"org-members-{Context.RunSuffix}", "Members Org").ConfigureAwait(false);

        Transcript.Describe("Outsider is denied members list");
        var members = await outsider.Client.GetAsync($"/organization/{org.Id}/members").ConfigureAwait(false);
        Assert.Equal(403, members.StatusCode);
        await Baselines.CaptureApiAsync("outsider-members-denied", members).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OwnerCanAddMemberByUsername()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"org-add-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var member = await identity.RegisterUserAsync($"org-add-member-{Context.RunSuffix}").ConfigureAwait(false);
        var org = await _organizations.CreateAsync(owner, $"org-add-{Context.RunSuffix}", "Add Org").ConfigureAwait(false);

        Transcript.Describe("Owner adds organization member");
        var add = await owner.Client.PostAsync($"/organization/{org.Id}/members", new
        {
            identifier = member.Username,
            role = 0,
        }).ConfigureAwait(false);
        Assert.Equal(204, add.StatusCode);
        await Baselines.CaptureApiAsync("add-member", add).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task AddedMemberCanListMembers()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"org-member-list-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var member = await identity.RegisterUserAsync($"org-member-list-member-{Context.RunSuffix}").ConfigureAwait(false);
        var org = await _organizations.CreateAsync(owner, $"org-member-list-{Context.RunSuffix}", "Member List Org").ConfigureAwait(false);
        await _organizations.AddMemberAsync(owner, org.Id, member.Username).ConfigureAwait(false);

        Transcript.Describe("Organization member can list members");
        var members = await member.Client.GetAsync($"/organization/{org.Id}/members").ConfigureAwait(false);
        Assert.Equal(200, members.StatusCode);
        Assert.Contains(member.Username, members.Body, StringComparison.Ordinal);
        await Baselines.CaptureApiAsync("member-list-members", members).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task MemberCanCreateRepositoryForOrganization()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"org-repo-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var member = await identity.RegisterUserAsync($"org-repo-member-{Context.RunSuffix}").ConfigureAwait(false);
        var orgSlug = $"org-repo-{Context.RunSuffix}";
        var org = await _organizations.CreateAsync(owner, orgSlug, "Repo Org").ConfigureAwait(false);
        await _organizations.AddMemberAsync(owner, org.Id, member.Username).ConfigureAwait(false);

        Transcript.Describe("Organization member creates repository under organization");
        var createRepo = await member.Client.PostAsync($"/repository/org-repo-{Context.RunSuffix}", new
        {
            repositoryName = "Org repo smoke",
            isPrivate = false,
            organizationSlug = orgSlug,
        }).ConfigureAwait(false);
        Assert.Equal(201, createRepo.StatusCode);
        await Baselines.CaptureApiAsync("organization-repo-create", createRepo).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OutsiderCannotCreateOrganizationRepository()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"org-repo-denied-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"org-repo-denied-outsider-{Context.RunSuffix}").ConfigureAwait(false);
        var orgSlug = $"org-repo-denied-{Context.RunSuffix}";
        await _organizations.CreateAsync(owner, orgSlug, "Denied Org").ConfigureAwait(false);

        Transcript.Describe("Outsider is forbidden from organization repo creation");
        var createRepo = await outsider.Client.PostAsync($"/repository/denied-{Context.RunSuffix}", new
        {
            repositoryName = "Denied repo",
            isPrivate = true,
            organizationSlug = orgSlug,
        }).ConfigureAwait(false);
        Assert.Equal(403, createRepo.StatusCode);
        await Baselines.CaptureApiAsync("outsider-org-repo-denied", createRepo).ConfigureAwait(false);
    }
}
