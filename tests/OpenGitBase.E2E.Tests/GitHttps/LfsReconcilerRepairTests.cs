using System.Security.Cryptography;
using System.Text;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.GitHttps;

/// <summary>
/// Lost LFS copies heal from what is left on disk (lfs-05). An encrypted replica that lost its LFS directory while
/// the database still lists its copy gets it back when an API instance restarts and runs the reconciler. When
/// every plaintext copy is gone, a clone gets 503 with Retry-After, which queues a repair that decrypts the object
/// from an encrypted replica, and git-lfs's retry then succeeds.
/// </summary>
[Collection("Compose")]
[Trait("Category", "GitHttps")]
[Trait("RequiresCompose", "true")]
[E2eTier(2)]
public class LfsReconcilerRepairTests : E2eTestBase
{
    private const string RestartedApi = "api-1";
    private static readonly TimeSpan CopyDeadline = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan RepairDeadline = TimeSpan.FromMinutes(3);

    [RequiresComposeFact]
    public async Task LostLfsCopiesAreRestoredFromTheCopiesLeftOnDisk()
    {
        BeginScenario();
        var gitOps = new GitOperations(Transcript);
        var chaos = new ClusterChaos(Transcript);
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-lfs-repair-{Context.RunSuffix}");
        var cloneDir = Path.Combine(Path.GetTempPath(), $"e2e-lfs-repair-clone-{Context.RunSuffix}");

        try
        {
            await LfsGit.RequireGitLfsAsync(gitOps).ConfigureAwait(false);

            Transcript.Describe("Owner pushes an LFS object to a repository with encrypted replicas");
            var identity = new IdentityFixture(Context, Transcript);
            var owner = await identity.RegisterUserAsync($"lfs-repair-{Context.RunSuffix}").ConfigureAwait(false);
            var admin = await identity.LoginPlatformAdminAsync().ConfigureAwait(false);
            var repository = await new RepositoryFixture(Transcript, Context.Normalizer)
                .CreateAsync(owner, "lfs-repair-repo", "LFS Repair Repo", isPrivate: true)
                .ConfigureAwait(false);
            await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);
            var replicas = await LfsStorageDisk.GetReplicasAsync(admin, repository.RepositoryId).ConfigureAwait(false);
            var encrypted = replicas.Where(replica => replica.Role == "EncryptedReplica").Select(replica => replica.NodeId).ToList();
            var plaintext = replicas
                .Where(replica => replica.Role != "EncryptedReplica")
                .Select(replica => replica.NodeId)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            Assert.Equal(2, encrypted.Count);

            // A binary prefix keeps git-lfs from copying the body to stderr under GIT_CURL_VERBOSE.
            byte[] content =
            [
                0, .. RandomNumberGenerator.GetBytes(511),
                .. Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat($"repair-{Context.RunSuffix}-", 80_000))),
            ];
            var oid = Convert.ToHexStringLower(SHA256.HashData(content));
            await LfsGit.InitLfsRepositoryAsync(gitOps, workDir, owner.Username).ConfigureAwait(false);
            await File.WriteAllBytesAsync(Path.Combine(workDir, "asset.bin"), content).ConfigureAwait(false);
            await gitOps.CommitPathsAsync(workDir, "add an lfs object").ConfigureAwait(false);
            var remote = await LfsGit.PatRemoteAsync(Context, owner, repository).ConfigureAwait(false);
            await gitOps.AddRemoteAsync(workDir, "origin", remote).ConfigureAwait(false);
            var push = await LfsGit.PushAsync(gitOps, workDir, "main").ConfigureAwait(false);
            Assert.True(push.Succeeded, LfsGit.WithoutTrace(push.StdErr));
            var relativePath = LfsStorageDisk.RelativePath(repository.RepositoryId, oid);
            var encryptedCopy = $"{LfsStorageDisk.EncryptedLfsRoot}/{relativePath}.aead";
            foreach (var node in encrypted)
            {
                await LfsStorageDisk.WaitForFileAsync(node, encryptedCopy, CopyDeadline).ConfigureAwait(false);
            }

            Transcript.Describe("An encrypted replica loses its LFS directory while the database still lists its copy");
            var lost = await LfsStorageDisk
                .ExecAsync(encrypted[0], "rm", "-rf", $"{LfsStorageDisk.EncryptedLfsRoot}/{repository.RepositoryId.ToLowerInvariant()}")
                .ConfigureAwait(false);
            Assert.True(lost.ExitCode == 0, lost.StdErr);

            Transcript.Describe("Restarting an API instance runs the reconciler, which re-encrypts the copy from plaintext");
            await chaos.StopServiceAsync(RestartedApi).ConfigureAwait(false);
            await chaos.StartServiceAsync(RestartedApi).ConfigureAwait(false);
            await LfsStorageDisk.WaitForHealthyAsync(RestartedApi, RepairDeadline).ConfigureAwait(false);
            await LfsStorageDisk.WaitForFileAsync(encrypted[0], encryptedCopy, RepairDeadline).ConfigureAwait(false);

            Transcript.Describe("Every plaintext copy is lost; a clone gets 503 and retries while the API decrypts one back");
            foreach (var node in plaintext)
            {
                var removed = await LfsStorageDisk
                    .ExecAsync(node, "rm", "-rf", $"{LfsStorageDisk.PlaintextLfsRoot}/{repository.RepositoryId.ToLowerInvariant()}")
                    .ConfigureAwait(false);
                Assert.True(removed.ExitCode == 0, removed.StdErr);
            }

            var clone = await LfsGit.CloneAsync(gitOps, remote, cloneDir).ConfigureAwait(false);
            Assert.True(clone.Succeeded, LfsGit.WithoutTrace(clone.StdErr));
            Assert.Contains("< HTTP/1.1 503", clone.StdErr, StringComparison.Ordinal);
            var cloned = await File.ReadAllBytesAsync(Path.Combine(cloneDir, "asset.bin")).ConfigureAwait(false);
            Assert.Equal(oid, Convert.ToHexStringLower(SHA256.HashData(cloned)));
            foreach (var node in plaintext)
            {
                await LfsStorageDisk
                    .WaitForFileAsync(node, $"{LfsStorageDisk.PlaintextLfsRoot}/{relativePath}", CopyDeadline)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            await chaos.RestoreAllAsync().ConfigureAwait(false);
            foreach (var directory in new[] { workDir, cloneDir })
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
