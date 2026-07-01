using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Repository;

[Collection("Compose")]
[Trait("Category", "Repository")]
[Trait("Tag", "Smoke")]
[Trait("RequiresCompose", "true")]
[E2eTier(5)]
public class BrowseSmokeTests : E2eTestBase
{
    private readonly GitTestDataFixture _gitData;
    private readonly RepositoryFixture _repositories;

    public BrowseSmokeTests()
    {
        _gitData = new GitTestDataFixture(Transcript, Context.Normalizer);
        _repositories = new RepositoryFixture(Transcript, Context.Normalizer);
    }

    [RequiresComposeFact]
    public async Task EmptyRepositoryRefsReturnSuccess()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"browse-empty-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var slug = $"browse-empty-{Context.RunSuffix}";
        await _repositories.CreateAsync(owner, slug, "Empty browse repo", isPrivate: false).ConfigureAwait(false);
        var anon = new E2eApiClient(Transcript, Context.Normalizer);

        Transcript.Describe("Empty repository refs endpoint returns 200");
        var refs = await anon.GetAsync($"/repository/by-slug/{owner.Username}/{slug}/content/refs").ConfigureAwait(false);
        Assert.Equal(200, refs.StatusCode);
        await Baselines.CaptureApiAsync("empty-refs", refs).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task ReadmeEndpointReturnsFixtureContent()
    {
        BeginScenario();
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-browse-readme-{Context.RunSuffix}");
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"browse-readme-owner-{Context.RunSuffix}").ConfigureAwait(false);

        try
        {
            var seeded = await _gitData.GetBrowsePublicRepoAsync(owner, Context.RunSuffix, workDir).ConfigureAwait(false);
            var anon = new E2eApiClient(Transcript, Context.Normalizer);

            Transcript.Describe("README endpoint returns seeded README blob");
            var readme = await anon.GetAsync($"/repository/by-slug/{seeded.OwnerUsername}/{seeded.Slug}/content/readme?refName=main")
                .ConfigureAwait(false);
            Assert.Equal(200, readme.StatusCode);
            Assert.Contains("README.md", readme.Body, StringComparison.Ordinal);
            Assert.Contains("browse-fixture", readme.Body, StringComparison.Ordinal);
            await Baselines.CaptureApiAsync("readme-endpoint", readme).ConfigureAwait(false);
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, true);
            }
        }
    }

    [RequiresComposeFact]
    public async Task PublicContentUsesCacheableHeaders()
    {
        BeginScenario();
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-browse-cache-public-{Context.RunSuffix}");
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"browse-cache-owner-{Context.RunSuffix}").ConfigureAwait(false);

        try
        {
            var seeded = await _gitData.GetBrowsePublicRepoAsync(owner, Context.RunSuffix, workDir).ConfigureAwait(false);
            var anon = new E2eApiClient(Transcript, Context.Normalizer);
            var relativeUrl = $"repository/by-slug/{seeded.OwnerUsername}/{seeded.Slug}/content/readme?refName=main";

            Transcript.Describe("Public browse endpoint includes cache-control public");
            using var response = await anon.RawClient.GetAsync(relativeUrl).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Assert.Equal(200, (int)response.StatusCode);
            Assert.True(response.Headers.CacheControl?.Public == true);
            Assert.Equal(60, response.Headers.CacheControl?.MaxAge?.TotalSeconds);
            await Baselines.CaptureApiAsync("public-cache-header", new HttpCapture
            {
                StatusCode = (int)response.StatusCode,
                Body = body,
                Method = "GET",
                Url = relativeUrl,
            }).ConfigureAwait(false);
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, true);
            }
        }
    }

    [RequiresComposeFact]
    public async Task PrivateContentUsesNoStoreHeaders()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"browse-cache-private-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var slug = $"browse-private-{Context.RunSuffix}";
        await _repositories.CreateAsync(owner, slug, "Private browse repo", isPrivate: true).ConfigureAwait(false);
        var relativeUrl = $"repository/by-slug/{owner.Username}/{slug}/content/refs";

        Transcript.Describe("Private browse endpoint includes cache-control no-store");
        using var response = await owner.Client.RawClient.GetAsync(relativeUrl).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(200, (int)response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        await Baselines.CaptureApiAsync("private-cache-header", new HttpCapture
        {
            StatusCode = (int)response.StatusCode,
            Body = body,
            Method = "GET",
            Url = relativeUrl,
        }).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task RawBlobDownloadReturnsFilePayload()
    {
        BeginScenario();
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-browse-raw-{Context.RunSuffix}");
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"browse-raw-owner-{Context.RunSuffix}").ConfigureAwait(false);

        try
        {
            var seeded = await _gitData.GetBrowsePublicRepoAsync(owner, Context.RunSuffix, workDir).ConfigureAwait(false);
            var anon = new E2eApiClient(Transcript, Context.Normalizer);
            var relativeUrl =
                $"repository/by-slug/{seeded.OwnerUsername}/{seeded.Slug}/content/blob/raw?refName=main&path={Uri.EscapeDataString(GitTestDataLayout.ReadmePath)}";

            Transcript.Describe("Raw blob endpoint serves octet-stream bytes");
            using var response = await anon.RawClient.GetAsync(relativeUrl).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Assert.Equal(200, (int)response.StatusCode);
            Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains(GitTestDataLayout.ReadmeContent.Trim(), body, StringComparison.Ordinal);
            await Baselines.CaptureApiAsync("raw-blob-download", new HttpCapture
            {
                StatusCode = (int)response.StatusCode,
                Body = body,
                Method = "GET",
                Url = relativeUrl,
            }).ConfigureAwait(false);
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, true);
            }
        }
    }
}
