using System.Security.Cryptography;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.GitHttps;

/// <summary>
/// A push never publishes a ref to an LFS object the server does not hold (lfs-04): a push whose objects were not
/// uploaded is rejected with the oid, path and fix; after <c>git lfs push --all</c> it is accepted, and a branch
/// that reuses those pointers needs no upload.
/// </summary>
[Collection("Compose")]
[Trait("Category", "GitHttps")]
[Trait("RequiresCompose", "true")]
[E2eTier(2)]
public class LfsPointerGateTests : E2eTestBase
{
    private static readonly IReadOnlyDictionary<string, string> SkipLfsUpload = new Dictionary<string, string>
    {
        ["GIT_LFS_SKIP_PUSH"] = "1",
        ["GIT_TERMINAL_PROMPT"] = "0",
    };

    [RequiresComposeFact]
    public async Task PushWithoutItsLfsObjectsIsRejectedUntilTheyAreUploaded()
    {
        BeginScenario();
        var gitOps = new GitOperations(Transcript);
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-lfs-gate-{Context.RunSuffix}");

        try
        {
            await LfsGit.RequireGitLfsAsync(gitOps).ConfigureAwait(false);

            Transcript.Describe("Owner commits an LFS file");
            var identity = new IdentityFixture(Context, Transcript);
            var owner = await identity.RegisterUserAsync($"lfs-gate-{Context.RunSuffix}").ConfigureAwait(false);
            var repository = await new RepositoryFixture(Transcript, Context.Normalizer)
                .CreateAsync(owner, "lfs-gate-repo", "LFS Gate Repo", isPrivate: false)
                .ConfigureAwait(false);
            await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);
            await LfsGit.InitLfsRepositoryAsync(gitOps, workDir, owner.Username).ConfigureAwait(false);
            var content = RandomNumberGenerator.GetBytes(64 * 1024);
            var oid = Convert.ToHexStringLower(SHA256.HashData(content));
            Directory.CreateDirectory(Path.Combine(workDir, "assets"));
            await File.WriteAllBytesAsync(Path.Combine(workDir, "assets", "big.bin"), content).ConfigureAwait(false);
            await gitOps.CommitPathsAsync(workDir, "add an lfs asset").ConfigureAwait(false);
            await gitOps.AddRemoteAsync(workDir, "origin", await LfsGit.PatRemoteAsync(Context, owner, repository).ConfigureAwait(false))
                .ConfigureAwait(false);

            Transcript.Describe("Pushing without uploading the object is rejected with oid, path and fix");
            var withheld = await PushAsync(gitOps, workDir, "main").ConfigureAwait(false);
            Assert.False(withheld.Succeeded, "A push whose LFS object is missing on the server must be rejected.");
            Assert.Contains($"LFS object {oid} (assets/big.bin) is missing on the server", withheld.StdErr, StringComparison.Ordinal);
            Assert.Contains("git lfs push --all origin main", withheld.StdErr, StringComparison.Ordinal);

            Transcript.Describe("After git lfs push --all the same push is accepted");
            var upload = await gitOps
                .RunAsync(workDir, ["-c", "credential.helper=", "lfs", "push", "--all", "origin", "main"], LfsGit.HttpTrace)
                .ConfigureAwait(false);
            Assert.True(upload.Succeeded, LfsGit.WithoutTrace(upload.StdErr));
            var accepted = await PushAsync(gitOps, workDir, "main").ConfigureAwait(false);
            Assert.True(accepted.Succeeded, accepted.StdErr);

            Transcript.Describe("A branch that reuses the pointer is accepted without uploading again");
            await gitOps.CheckoutBranchAsync(workDir, "feature", create: true).ConfigureAwait(false);
            File.Copy(Path.Combine(workDir, "assets", "big.bin"), Path.Combine(workDir, "assets", "copy.bin"));
            await gitOps.CommitPathsAsync(workDir, "reuse the lfs asset").ConfigureAwait(false);
            var branch = await PushAsync(gitOps, workDir, "feature").ConfigureAwait(false);
            Assert.True(branch.Succeeded, branch.StdErr);
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, true);
            }
        }
    }

    /// <summary>A plain git push whose pre-push hook skips the LFS upload (<c>GIT_LFS_SKIP_PUSH</c>).</summary>
    private static Task<GitCommandResult> PushAsync(GitOperations gitOps, string workDir, string branch) =>
        gitOps.RunAsync(workDir, ["-c", "credential.helper=", "push", "-u", "origin", branch], SkipLfsUpload);
}
