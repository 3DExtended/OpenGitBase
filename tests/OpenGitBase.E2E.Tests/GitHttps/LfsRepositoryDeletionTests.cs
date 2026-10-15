using System.Security.Cryptography;
using System.Text;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.GitHttps;

/// <summary>
/// Deleting a repository removes its LFS data (lfs-08): once the delete returns, no storage node keeps the
/// repository's plaintext or encrypted LFS directory.
/// </summary>
[Collection("Compose")]
[Trait("Category", "GitHttps")]
[Trait("RequiresCompose", "true")]
[E2eTier(2)]
public class LfsRepositoryDeletionTests : E2eTestBase
{
    private static readonly string[] StorageNodes = ["storage-1", "storage-2", "storage-3", "storage-4"];
    private static readonly TimeSpan CopyDeadline = TimeSpan.FromSeconds(180);
    private static readonly TimeSpan CleanupDeadline = TimeSpan.FromSeconds(30);

    [RequiresComposeFact]
    public async Task DeletingARepositoryRemovesItsLfsDataFromEveryNode()
    {
        BeginScenario();
        var gitOps = new GitOperations(Transcript);
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-lfs-delete-{Context.RunSuffix}");

        try
        {
            await LfsGit.RequireGitLfsAsync(gitOps).ConfigureAwait(false);

            Transcript.Describe("Owner pushes an LFS object and every replica gets its copy");
            var identity = new IdentityFixture(Context, Transcript);
            var owner = await identity.RegisterUserAsync($"lfs-delete-{Context.RunSuffix}").ConfigureAwait(false);
            var admin = await identity.LoginPlatformAdminAsync().ConfigureAwait(false);
            var repository = await new RepositoryFixture(Transcript, Context.Normalizer)
                .CreateAsync(owner, "lfs-delete-repo", "LFS Delete Repo", isPrivate: true)
                .ConfigureAwait(false);
            await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);

            // A binary prefix keeps git-lfs from copying the body to stderr under GIT_CURL_VERBOSE.
            byte[] content =
            [
                0, .. RandomNumberGenerator.GetBytes(511),
                .. Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat($"delete-{Context.RunSuffix}-", 20_000))),
            ];
            var oid = Convert.ToHexStringLower(SHA256.HashData(content));
            await LfsGit.InitLfsRepositoryAsync(gitOps, workDir, owner.Username).ConfigureAwait(false);
            await File.WriteAllBytesAsync(Path.Combine(workDir, "asset.bin"), content).ConfigureAwait(false);
            await gitOps.CommitPathsAsync(workDir, "add an lfs object").ConfigureAwait(false);
            await gitOps.AddRemoteAsync(workDir, "origin", await LfsGit.PatRemoteAsync(Context, owner, repository).ConfigureAwait(false))
                .ConfigureAwait(false);
            var push = await LfsGit.PushAsync(gitOps, workDir, "main").ConfigureAwait(false);
            Assert.True(push.Succeeded, LfsGit.WithoutTrace(push.StdErr));

            // Waiting for every copy keeps a late asynchronous copy from landing after the delete.
            var relativePath = LfsStorageDisk.RelativePath(repository.RepositoryId, oid);
            foreach (var (nodeId, role) in await LfsStorageDisk.GetReplicasAsync(admin, repository.RepositoryId).ConfigureAwait(false))
            {
                var copy = role == "EncryptedReplica"
                    ? $"{LfsStorageDisk.EncryptedLfsRoot}/{relativePath}.aead"
                    : $"{LfsStorageDisk.PlaintextLfsRoot}/{relativePath}";
                await LfsStorageDisk.WaitForFileAsync(nodeId, copy, CopyDeadline).ConfigureAwait(false);
            }

            Transcript.Describe("Owner deletes the repository");
            var delete = await owner.Client.SendAsync(HttpMethod.Delete, $"/repository/{repository.RepositoryId}").ConfigureAwait(false);
            Assert.True(delete.StatusCode == 204, $"DELETE repository: {delete.StatusCode} {delete.Body}");

            Transcript.Describe("No storage node keeps an LFS directory of the deleted repository");
            var directory = repository.RepositoryId.ToLowerInvariant();
            foreach (var nodeId in StorageNodes)
            {
                await LfsStorageDisk
                    .WaitForAbsentAsync(nodeId, $"{LfsStorageDisk.PlaintextLfsRoot}/{directory}", CleanupDeadline)
                    .ConfigureAwait(false);
                await LfsStorageDisk
                    .WaitForAbsentAsync(nodeId, $"{LfsStorageDisk.EncryptedLfsRoot}/{directory}", CleanupDeadline)
                    .ConfigureAwait(false);
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
