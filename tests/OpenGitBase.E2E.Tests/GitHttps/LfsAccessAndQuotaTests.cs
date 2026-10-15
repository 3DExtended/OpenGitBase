using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.GitHttps;

/// <summary>
/// Git LFS follows the git access rules and the repository storage quota (lfs-02): a read-only member pulls
/// but cannot push LFS objects, and an upload beyond the quota fails with the server's 507 message.
/// </summary>
[Collection("Compose")]
[Trait("Category", "GitHttps")]
[Trait("RequiresCompose", "true")]
[E2eTier(2)]
public class LfsAccessAndQuotaTests : E2eTestBase
{
    private const int ReaderRole = 1;

    [RequiresComposeFact]
    public async Task ReadOnlyMemberPullsButCannotPushLfsObjects()
    {
        BeginScenario();
        var gitOps = new GitOperations(Transcript);
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-lfs-reader-{Context.RunSuffix}");

        try
        {
            await LfsGit.RequireGitLfsAsync(gitOps).ConfigureAwait(false);

            Transcript.Describe("Owner creates a private repository and adds a read-only member");
            var identity = new IdentityFixture(Context, Transcript);
            var owner = await identity.RegisterUserAsync($"lfs-owner-{Context.RunSuffix}").ConfigureAwait(false);
            var reader = await identity.RegisterUserAsync($"lfs-reader-{Context.RunSuffix}").ConfigureAwait(false);
            var repositories = new RepositoryFixture(Transcript, Context.Normalizer);
            var repository = await repositories
                .CreateAsync(owner, "lfs-reader-repo", "LFS Reader Repo", isPrivate: true)
                .ConfigureAwait(false);
            await repositories.AddMemberAsync(owner, repository.RepositoryId, reader.UserId, ReaderRole).ConfigureAwait(false);
            await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);
            var ownerRemote = await RemoteForAsync(owner, repository).ConfigureAwait(false);
            var readerRemote = await RemoteForAsync(reader, repository).ConfigureAwait(false);

            Transcript.Describe("Owner pushes a 1 MB LFS object");
            var ownerDir = Path.Combine(workDir, "owner");
            await LfsGit.InitLfsRepositoryAsync(gitOps, ownerDir, owner.Username).ConfigureAwait(false);
            var content = RandomNumberGenerator.GetBytes(1024 * 1024);
            await File.WriteAllBytesAsync(Path.Combine(ownerDir, "asset.bin"), content).ConfigureAwait(false);
            await gitOps.CommitPathsAsync(ownerDir, "add lfs asset").ConfigureAwait(false);
            await gitOps.AddRemoteAsync(ownerDir, "origin", ownerRemote).ConfigureAwait(false);
            var ownerPush = await LfsGit.PushAsync(gitOps, ownerDir, "main").ConfigureAwait(false);
            Assert.True(ownerPush.Succeeded, ownerPush.StdErr);

            Transcript.Describe("Repository usage reports the LFS bytes separately");
            var usage = await owner.Client.GetAsync($"repository/{repository.RepositoryId}/usage").ConfigureAwait(false);
            Assert.Equal(200, usage.StatusCode);
            using (var usageJson = JsonDocument.Parse(usage.Body))
            {
                Assert.Equal(content.Length, usageJson.RootElement.GetProperty("lfsBytes").GetInt64());
                Assert.True(usageJson.RootElement.TryGetProperty("gitBytes", out _));
            }

            Transcript.Describe("Reader clones: the LFS object downloads byte-identical");
            var readerDir = Path.Combine(workDir, "reader");
            var clone = await LfsGit.CloneAsync(gitOps, readerRemote, readerDir).ConfigureAwait(false);
            Assert.True(clone.Succeeded, clone.StdErr);
            var cloned = await File.ReadAllBytesAsync(Path.Combine(readerDir, "asset.bin")).ConfigureAwait(false);
            Assert.Equal(SHA256.HashData(content), SHA256.HashData(cloned));

            Transcript.Describe("Reader's LFS upload is refused with 403");
            await LfsGit.RunAsync(gitOps, readerDir, ["lfs", "install", "--local"]).ConfigureAwait(false);
            await LfsGit.RunAsync(gitOps, readerDir, ["config", "user.name", reader.Username]).ConfigureAwait(false);
            await LfsGit.RunAsync(gitOps, readerDir, ["config", "user.email", $"{reader.Username}@example.com"])
                .ConfigureAwait(false);
            await File.WriteAllBytesAsync(Path.Combine(readerDir, "other.bin"), RandomNumberGenerator.GetBytes(4096))
                .ConfigureAwait(false);
            await gitOps.CommitPathsAsync(readerDir, "reader adds an lfs asset").ConfigureAwait(false);
            // git push stops at receive-pack discovery for a reader; git lfs push asks the batch API directly.
            var readerPush = await gitOps
                .RunAsync(readerDir, ["-c", "credential.helper=", "lfs", "push", "origin", "main"], LfsGit.HttpTrace)
                .ConfigureAwait(false);
            var readerOutput = LfsGit.WithoutTrace(readerPush.StdErr);
            Assert.False(readerPush.Succeeded, "A read-only member must not be able to push LFS objects.");
            Assert.True(readerOutput.Contains("write access", StringComparison.OrdinalIgnoreCase), readerOutput);
            Assert.DoesNotContain("> PUT ", readerPush.StdErr, StringComparison.Ordinal);
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
    public async Task UploadBeyondTheRepositoryQuotaIsRefusedWith507()
    {
        BeginScenario();
        var gitOps = new GitOperations(Transcript);
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-lfs-quota-{Context.RunSuffix}");

        try
        {
            await LfsGit.RequireGitLfsAsync(gitOps).ConfigureAwait(false);

            Transcript.Describe("Repository is 2 MiB short of its storage limit");
            var identity = new IdentityFixture(Context, Transcript);
            var owner = await identity.RegisterUserAsync($"lfs-quota-{Context.RunSuffix}").ConfigureAwait(false);
            var repository = await new RepositoryFixture(Transcript, Context.Normalizer)
                .CreateAsync(owner, "lfs-quota-repo", "LFS Quota Repo", isPrivate: false)
                .ConfigureAwait(false);
            await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);
            var usage = await owner.Client.GetAsync($"repository/{repository.RepositoryId}/usage").ConfigureAwait(false);
            long bytesLimit;
            using (var usageJson = JsonDocument.Parse(usage.Body))
            {
                bytesLimit = usageJson.RootElement.GetProperty("bytesLimit").GetInt64();
            }

            // Per-repository overrides need an organization-run storage fleet, so fill the usage instead.
            await SetStorageBytesUsedAsync(repository.RepositoryId, bytesLimit - (2 * 1024 * 1024)).ConfigureAwait(false);
            var remote = await RemoteForAsync(owner, repository).ConfigureAwait(false);

            Transcript.Describe("Pushing a 3 MiB LFS object fails with the quota message");
            await LfsGit.InitLfsRepositoryAsync(gitOps, workDir, owner.Username).ConfigureAwait(false);
            await File.WriteAllBytesAsync(Path.Combine(workDir, "big.bin"), RandomNumberGenerator.GetBytes(3 * 1024 * 1024))
                .ConfigureAwait(false);
            await gitOps.CommitPathsAsync(workDir, "add an lfs asset above the quota").ConfigureAwait(false);
            await gitOps.AddRemoteAsync(workDir, "origin", remote).ConfigureAwait(false);
            var push = await LfsGit.PushAsync(gitOps, workDir, "main").ConfigureAwait(false);
            var output = LfsGit.WithoutTrace(push.StdErr);
            Assert.False(push.Succeeded, "An LFS upload beyond the repository quota must fail.");
            Assert.True(output.Contains("quota exceeded", StringComparison.OrdinalIgnoreCase), output);
            Assert.DoesNotContain("> PUT ", push.StdErr, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, true);
            }
        }
    }

    private static async Task SetStorageBytesUsedAsync(string repositoryId, long bytes)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in new[]
        {
            "exec", E2eEnvironment.ContainerName("postgres"), "psql", "-U", "postgres", "-d", "opengitbase", "-v",
            "ON_ERROR_STOP=1", "-c",
            $"UPDATE \"Repository\" SET \"StorageBytesUsed\" = {bytes} WHERE \"Id\" = '{Guid.Parse(repositoryId)}'",
        })
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start docker.");
        var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        Assert.True(process.ExitCode == 0 && stdout.Contains("UPDATE 1", StringComparison.Ordinal), stderr + stdout);
    }

    private async Task<string> RemoteForAsync(AuthenticatedClient user, RepositorySeed repository)
    {
        var patResponse = await user.Client
            .PostAsync("/git-access-token", new { name = $"lfs-{user.Username}", scope = "write" })
            .ConfigureAwait(false);
        var pat = E2eScenarioHelpers.ParsePatToken(patResponse);
        Context.Normalizer.RegisterToken("PAT", pat);
        return E2eEnvironment.BuildPatRemoteUrl(repository.OwnerUsername, repository.Slug, pat);
    }
}
