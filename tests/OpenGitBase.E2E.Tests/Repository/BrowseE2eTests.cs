using System.Text.Json;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Repository;

[Collection("Compose")]
[Trait("Category", "Repository")]
[Trait("RequiresCompose", "true")]
[E2eTier(5)]
public class BrowseE2eTests : E2eTestBase
{
    private readonly GitTestDataFixture _gitData;

    public BrowseE2eTests()
    {
        _gitData = new GitTestDataFixture(Transcript, Context.Normalizer);
    }

    [RequiresComposeFact]
    public async Task PublicRefsAndTreeAnonymousAccess()
    {
        BeginScenario();
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-browse-{Context.RunSuffix}");
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"browse-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var browse = await _gitData.GetBrowsePublicRepoAsync(owner, Context.RunSuffix, workDir).ConfigureAwait(false);
        var privateSlug = $"browse-private-{Context.RunSuffix}";
        var repos = new RepositoryFixture(Transcript, Context.Normalizer);
        await repos.CreateAsync(owner, privateSlug, "Browse Private", isPrivate: true).ConfigureAwait(false);

        try
        {
            var anon = new E2eApiClient(Transcript, Context.Normalizer);
            var refsUrl = $"/repository/by-slug/{browse.OwnerUsername}/{browse.Slug}/content/refs";
            Transcript.Describe("Anonymous public refs returns 200");
            var refs = await anon.GetAsync(refsUrl).ConfigureAwait(false);
            Assert.Equal(200, refs.StatusCode);
            await Baselines.CaptureApiAsync("public-refs", refs).ConfigureAwait(false);

            Transcript.Describe("Anonymous public tree returns 200");
            var tree = await anon.GetAsync($"/repository/by-slug/{browse.OwnerUsername}/{browse.Slug}/content/tree?refName=main&path=").ConfigureAwait(false);
            Assert.Equal(200, tree.StatusCode);
            await Baselines.CaptureApiAsync("public-tree", tree).ConfigureAwait(false);

            Transcript.Describe("Anonymous private refs returns 404");
            var privateRefs = await anon.GetAsync($"/repository/by-slug/{browse.OwnerUsername}/{privateSlug}/content/refs").ConfigureAwait(false);
            Assert.Equal(404, privateRefs.StatusCode);
            await Baselines.CaptureApiAsync("private-anon-404", privateRefs).ConfigureAwait(false);
            await AssertBaselinesAsync().ConfigureAwait(false);
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
    public async Task KnownFileTreeIncludesNestedSvgAndLargeBlob()
    {
        BeginScenario();
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-browse-tree-{Context.RunSuffix}");
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"browse-tree-{Context.RunSuffix}").ConfigureAwait(false);
        var browse = await _gitData.GetBrowsePublicRepoAsync(owner, Context.RunSuffix, workDir).ConfigureAwait(false);
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var baseUrl = $"/repository/by-slug/{browse.OwnerUsername}/{browse.Slug}/content";

        try
        {
            Transcript.Describe("Root tree lists README from git testdata fixture");
            var rootTree = await anon.GetAsync($"{baseUrl}/tree?refName=main&path=").ConfigureAwait(false);
            Assert.Equal(200, rootTree.StatusCode);
            Assert.Contains(GitTestDataLayout.ReadmePath, rootTree.Body, StringComparison.Ordinal);

            Transcript.Describe("Nested path tree exposes bar.txt");
            var nestedTree = await anon.GetAsync($"{baseUrl}/tree?refName=main&path=src/foo").ConfigureAwait(false);
            Assert.Equal(200, nestedTree.StatusCode);
            Assert.Contains("bar.txt", nestedTree.Body, StringComparison.Ordinal);

            Transcript.Describe("README blob matches fixture content");
            var readmeBlob = await anon.GetAsync($"{baseUrl}/blob?refName=main&path={GitTestDataLayout.ReadmePath}").ConfigureAwait(false);
            Assert.Equal(200, readmeBlob.StatusCode);
            using (var readmeDoc = JsonDocument.Parse(readmeBlob.Body))
            {
                Assert.Contains(
                    GitTestDataLayout.ReadmeContent.Trim(),
                    readmeDoc.RootElement.GetProperty("textContent").GetString() ?? string.Empty,
                    StringComparison.Ordinal);
            }

            Transcript.Describe("SVG asset is classified with previewKind svg");
            var svgBlob = await anon.GetAsync($"{baseUrl}/blob?refName=main&path={GitTestDataLayout.SvgPath}").ConfigureAwait(false);
            Assert.Equal(200, svgBlob.StatusCode);
            using (var svgDoc = JsonDocument.Parse(svgBlob.Body))
            {
                Assert.Equal("svg", svgDoc.RootElement.GetProperty("previewKind").GetString());
            }

            Transcript.Describe("Oversized blob is flagged isTooLarge");
            var largeBlob = await anon.GetAsync($"{baseUrl}/blob?refName=main&path={GitTestDataLayout.LargeBlobPath}").ConfigureAwait(false);
            Assert.Equal(200, largeBlob.StatusCode);
            using (var largeDoc = JsonDocument.Parse(largeBlob.Body))
            {
                Assert.True(largeDoc.RootElement.GetProperty("isTooLarge").GetBoolean());
                Assert.True(largeDoc.RootElement.GetProperty("size").GetInt64() > GitTestDataLayout.LargeBlobSizeBytes - 1024);
            }

            await Baselines.CaptureApiAsync("nested-tree", nestedTree).ConfigureAwait(false);
            await AssertBaselinesAsync().ConfigureAwait(false);
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
    public async Task PublicCommitReadReturnsRootTreeForInitialCommit()
    {
        BeginScenario();
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-commit-read-{Context.RunSuffix}");
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"browse-commit-{Context.RunSuffix}").ConfigureAwait(false);
        var browse = await _gitData.GetBrowsePublicRepoAsync(owner, Context.RunSuffix, workDir).ConfigureAwait(false);
        var anon = new E2eApiClient(Transcript, Context.Normalizer);

        try
        {
            Transcript.Describe("Resolve main branch commit SHA from refs");
            var refs = await anon.GetAsync($"/repository/by-slug/{browse.OwnerUsername}/{browse.Slug}/content/refs").ConfigureAwait(false);
            Assert.Equal(200, refs.StatusCode);
            using var refsDoc = JsonDocument.Parse(refs.Body);
            var mainSha = refsDoc.RootElement.GetProperty("branches")[0].GetProperty("commitSha").GetString();
            Assert.False(string.IsNullOrWhiteSpace(mainSha));

            Transcript.Describe("Anonymous commit read returns metadata and root or diff payload");
            var commit = await anon.GetAsync($"/repository/by-slug/{browse.OwnerUsername}/{browse.Slug}/commits/{mainSha}").ConfigureAwait(false);
            Assert.Equal(200, commit.StatusCode);
            using var commitDoc = JsonDocument.Parse(commit.Body);
            Assert.Equal(mainSha, commitDoc.RootElement.GetProperty("sha").GetString());
            Assert.True(commitDoc.RootElement.TryGetProperty("kind", out _));
            await Baselines.CaptureApiAsync("public-commit-read", commit).ConfigureAwait(false);
            await AssertBaselinesAsync().ConfigureAwait(false);
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
