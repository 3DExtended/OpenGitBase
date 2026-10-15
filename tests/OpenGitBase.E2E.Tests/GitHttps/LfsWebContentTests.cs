using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.GitHttps;

/// <summary>
/// LFS files in the web UI (lfs-07): the blob endpoint describes the LFS object instead of the pointer, and the raw
/// endpoint streams the object itself with its type and length, under the repository's read permissions.
/// </summary>
[Collection("Compose")]
[Trait("Category", "GitHttps")]
[Trait("RequiresCompose", "true")]
[E2eTier(2)]
public class LfsWebContentTests : E2eTestBase
{
    private const string ImagePath = "assets/logo.png";

    [RequiresComposeFact]
    public async Task AnLfsImageIsDescribedByItsObjectAndDownloadsAsTheRealFile()
    {
        BeginScenario();
        var gitOps = new GitOperations(Transcript);
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-lfs-web-{Context.RunSuffix}");

        try
        {
            await LfsGit.RequireGitLfsAsync(gitOps).ConfigureAwait(false);

            Transcript.Describe("Owner pushes a PNG tracked with Git LFS to a private repository");
            var identity = new IdentityFixture(Context, Transcript);
            var owner = await identity.RegisterUserAsync($"lfs-web-{Context.RunSuffix}").ConfigureAwait(false);
            var outsider = await identity.RegisterUserAsync($"lfs-web-out-{Context.RunSuffix}").ConfigureAwait(false);
            var repository = await new RepositoryFixture(Transcript, Context.Normalizer)
                .CreateAsync(owner, "lfs-web-repo", "LFS Web Repo", isPrivate: true)
                .ConfigureAwait(false);
            await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);

            byte[] content = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, .. RandomNumberGenerator.GetBytes(600_000)];
            var oid = Convert.ToHexStringLower(SHA256.HashData(content));
            await LfsGit.InitLfsRepositoryAsync(gitOps, workDir, owner.Username).ConfigureAwait(false);
            await LfsGit.RunAsync(gitOps, workDir, ["lfs", "track", "*.png"]).ConfigureAwait(false);
            Directory.CreateDirectory(Path.Combine(workDir, "assets"));
            await File.WriteAllBytesAsync(Path.Combine(workDir, ImagePath), content).ConfigureAwait(false);
            await gitOps.CommitPathsAsync(workDir, "add an lfs image").ConfigureAwait(false);
            await gitOps.AddRemoteAsync(workDir, "origin", await LfsGit.PatRemoteAsync(Context, owner, repository).ConfigureAwait(false))
                .ConfigureAwait(false);
            var push = await LfsGit.PushAsync(gitOps, workDir, "main").ConfigureAwait(false);
            Assert.True(push.Succeeded, LfsGit.WithoutTrace(push.StdErr));

            var contentBase = $"repository/by-slug/{owner.Username}/{repository.Slug}/content";
            var query = $"refName=main&path={Uri.EscapeDataString(ImagePath)}";

            Transcript.Describe("The blob endpoint describes the LFS object, not the pointer text");
            using (var blob = await owner.Client.RawClient.GetAsync($"{contentBase}/blob?{query}").ConfigureAwait(false))
            {
                var body = await blob.Content.ReadAsStringAsync().ConfigureAwait(false);
                Assert.True(blob.IsSuccessStatusCode, $"GET blob: {(int)blob.StatusCode} {body}");
                using var document = JsonDocument.Parse(body);
                var lfs = document.RootElement.GetProperty("lfs");
                Assert.Equal(oid, lfs.GetProperty("oid").GetString());
                Assert.Equal(content.Length, lfs.GetProperty("size").GetInt64());
                Assert.True(lfs.GetProperty("available").GetBoolean());
                Assert.Equal("image", document.RootElement.GetProperty("previewKind").GetString());
                Assert.False(document.RootElement.TryGetProperty("textContent", out var text) && text.ValueKind == JsonValueKind.String);
            }

            Transcript.Describe("The raw endpoint streams the real file with its type and length");
            using (var raw = await owner.Client.RawClient
                       .GetAsync($"{contentBase}/blob/raw?{query}", HttpCompletionOption.ResponseHeadersRead)
                       .ConfigureAwait(false))
            {
                Assert.Equal(HttpStatusCode.OK, raw.StatusCode);
                Assert.Equal("image/png", raw.Content.Headers.ContentType?.MediaType);
                Assert.Equal(content.Length, raw.Content.Headers.ContentLength);
                Assert.Contains("nosniff", raw.Headers.GetValues("X-Content-Type-Options"));
                var downloaded = await raw.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                Assert.Equal(oid, Convert.ToHexStringLower(SHA256.HashData(downloaded)));
            }

            Transcript.Describe("A user without access to the private repository gets no LFS content");
            using (var refused = await outsider.Client.RawClient.GetAsync($"{contentBase}/blob/raw?{query}").ConfigureAwait(false))
            {
                Assert.True(
                    refused.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                    $"outsider raw: {(int)refused.StatusCode}"
                );
                Assert.NotEqual(content.Length, refused.Content.Headers.ContentLength);
            }
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
