using System.Security.Cryptography;
using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.GitHttps;

/// <summary>
/// Git LFS over HTTPS with the real git-lfs client (lfs-01): push stores the object, clone smudges it back
/// byte-identical, and objects the server already recorded are never uploaded again.
/// Needs git-lfs on the machine that runs the suite and <c>Lfs__Enabled=true</c> (docker-compose.e2e.yml).
/// </summary>
[Collection("Compose")]
[Trait("Category", "GitHttps")]
[Trait("RequiresCompose", "true")]
[E2eTier(2)]
public class LfsPushCloneTests : E2eTestBase
{
    // The filter config `git lfs install` would write globally, passed per command so the suite does not
    // depend on (or change) the machine's git config.
    private static readonly string[] LfsFilterConfig =
    [
        "-c", "filter.lfs.process=git-lfs filter-process",
        "-c", "filter.lfs.required=true",
        "-c", "filter.lfs.clean=git-lfs clean -- %f",
        "-c", "filter.lfs.smudge=git-lfs smudge -- %f",
    ];

    private static readonly Dictionary<string, string> HttpTrace = new()
    {
        ["GIT_CURL_VERBOSE"] = "1",
        ["GIT_TERMINAL_PROMPT"] = "0",
    };

    [RequiresComposeFact]
    public async Task LfsPushAndCloneRoundTripByteIdentical()
    {
        BeginScenario();
        var username = $"lfs-e2e-{Context.RunSuffix}";
        var email = $"{username}@example.com";
        var password = "Password123!";
        var repoSlug = "lfs-e2e-repo";
        var gitOps = new GitOperations(Transcript);
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-lfs-{Context.RunSuffix}");

        try
        {
            var lfsVersion = await gitOps.RunAsync(E2eEnvironment.RepoRoot, ["lfs", "version"]).ConfigureAwait(false);
            Assert.True(lfsVersion.Succeeded, "git-lfs must be installed on the machine that runs the E2E suite.");

            Transcript.Describe("Register user, create repository and a write PAT");
            var anon = new E2eApiClient(Transcript, Context.Normalizer);
            await anon.PostAsync("/register/register", new { username, email, password }).ConfigureAwait(false);
            var login = await anon.PostAsync("/signin/login", new { username, password }).ConfigureAwait(false);
            var client = new E2eApiClient(Transcript, Context.Normalizer, E2eScenarioHelpers.ParseJwtToken(login));
            await client.PostAsync("/account/debug/verify-email", null).ConfigureAwait(false);
            var createRepo = await client
                .PostAsync($"/repository/{repoSlug}", new { repositoryName = "LFS E2E Repo", isPrivate = false })
                .ConfigureAwait(false);
            Assert.Equal(201, createRepo.StatusCode);
            await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);
            var patResponse = await client
                .PostAsync("/git-access-token", new { name = "e2e-lfs", scope = "write" })
                .ConfigureAwait(false);
            var pat = E2eScenarioHelpers.ParsePatToken(patResponse);
            Context.Normalizer.RegisterToken("LFS_PAT", pat);
            var remote = E2eEnvironment.BuildPatRemoteUrl(username, repoSlug, pat);

            Transcript.Describe("Track *.bin with LFS and commit a 5 MB file");
            await gitOps.InitRepositoryAsync(workDir, "main", username, email).ConfigureAwait(false);
            await RunAsync(gitOps, workDir, ["lfs", "install", "--local"]).ConfigureAwait(false);
            await RunAsync(gitOps, workDir, ["lfs", "track", "*.bin"]).ConfigureAwait(false);
            var content = RandomNumberGenerator.GetBytes(5 * 1024 * 1024);
            await File.WriteAllBytesAsync(Path.Combine(workDir, "asset.bin"), content).ConfigureAwait(false);
            await gitOps.CommitPathsAsync(workDir, "add lfs asset").ConfigureAwait(false);
            var pointer = await RunAsync(gitOps, workDir, ["show", "HEAD:asset.bin"]).ConfigureAwait(false);
            Assert.StartsWith("version https://git-lfs.github.com/spec/v1", pointer.StdOut, StringComparison.Ordinal);
            await gitOps.AddRemoteAsync(workDir, "origin", remote).ConfigureAwait(false);

            Transcript.Describe("Push: git-lfs uploads the object through the dispatcher");
            var push = await gitOps
                .RunAsync(workDir, ["-c", "credential.helper=", "push", "-u", "origin", "main"], HttpTrace)
                .ConfigureAwait(false);
            Assert.True(push.Succeeded, push.StdErr);
            Assert.Contains("/info/lfs/objects/batch", push.StdErr, StringComparison.Ordinal);
            Assert.Contains("> PUT ", push.StdErr, StringComparison.Ordinal);
            Assert.DoesNotContain("locking API", push.StdErr, StringComparison.OrdinalIgnoreCase);

            Transcript.Describe("Clone into a new directory: the file comes back byte-identical");
            var cloneDir = Path.Combine(workDir, "clone");
            var clone = await gitOps
                .RunAsync(
                    E2eEnvironment.RepoRoot,
                    ["-c", "credential.helper=", .. LfsFilterConfig, "clone", remote, cloneDir],
                    HttpTrace
                )
                .ConfigureAwait(false);
            Assert.True(clone.Succeeded, clone.StdErr);
            var cloned = await File.ReadAllBytesAsync(Path.Combine(cloneDir, "asset.bin")).ConfigureAwait(false);
            Assert.Equal(SHA256.HashData(content), SHA256.HashData(cloned));
            Assert.Equal(content.Length, cloned.Length);

            Transcript.Describe("Push every LFS object again: the server already has them, nothing is uploaded");
            await gitOps.CheckoutBranchAsync(workDir, "feature", create: true).ConfigureAwait(false);
            var branchPush = await gitOps
                .RunAsync(workDir, ["-c", "credential.helper=", "push", "-u", "origin", "feature"], HttpTrace)
                .ConfigureAwait(false);
            Assert.True(branchPush.Succeeded, branchPush.StdErr);
            var lfsPushAll = await gitOps
                .RunAsync(workDir, ["-c", "credential.helper=", "lfs", "push", "--all", "origin"], HttpTrace)
                .ConfigureAwait(false);
            Assert.True(lfsPushAll.Succeeded, lfsPushAll.StdErr);
            Assert.Contains("/info/lfs/objects/batch", lfsPushAll.StdErr, StringComparison.Ordinal);
            Assert.DoesNotContain("> PUT ", branchPush.StdErr, StringComparison.Ordinal);
            Assert.DoesNotContain("> PUT ", lfsPushAll.StdErr, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, true);
            }
        }
    }

    private static async Task<GitCommandResult> RunAsync(GitOperations gitOps, string workDir, string[] args)
    {
        var result = await gitOps.RunAsync(workDir, args).ConfigureAwait(false);
        Assert.True(result.Succeeded, $"git {string.Join(' ', args)}: {result.StdErr}");
        return result;
    }
}
