using System.Security.Cryptography;
using System.Text;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.GitHttps;

/// <summary>
/// An acknowledged LFS upload reaches LFS Quorum (lfs-03): both encrypted replicas end up with a copy, the read
/// replica with the object, and no encrypted replica's disk holds a byte of plaintext.
/// </summary>
[Collection("Compose")]
[Trait("Category", "GitHttps")]
[Trait("RequiresCompose", "true")]
[E2eTier(2)]
public class LfsEncryptedReplicaTests : E2eTestBase
{
    private static readonly TimeSpan CopyDeadline = TimeSpan.FromSeconds(90);

    [RequiresComposeFact]
    public async Task EncryptedReplicasHoldOnlyCiphertextOfAnUploadedObject()
    {
        BeginScenario();
        var gitOps = new GitOperations(Transcript);
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-lfs-encrypted-{Context.RunSuffix}");

        try
        {
            await LfsGit.RequireGitLfsAsync(gitOps).ConfigureAwait(false);

            Transcript.Describe("Owner creates a repository with encrypted replicas");
            var identity = new IdentityFixture(Context, Transcript);
            var owner = await identity.RegisterUserAsync($"lfs-enc-{Context.RunSuffix}").ConfigureAwait(false);
            var admin = await identity.LoginPlatformAdminAsync().ConfigureAwait(false);
            var repository = await new RepositoryFixture(Transcript, Context.Normalizer)
                .CreateAsync(owner, "lfs-encrypted-repo", "LFS Encrypted Repo", isPrivate: true)
                .ConfigureAwait(false);
            await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);
            var replicas = await LfsStorageDisk.GetReplicasAsync(admin, repository.RepositoryId).ConfigureAwait(false);
            var encrypted = replicas.Where(replica => replica.Role == "EncryptedReplica").Select(replica => replica.NodeId).ToList();
            Assert.Equal(2, encrypted.Count);

            Transcript.Describe("Owner pushes a multi-segment LFS object full of a plaintext marker");
            var marker = $"OGB-LFS-PLAINTEXT-MARKER-{Context.RunSuffix}-";
            // A binary prefix keeps git-lfs from sending the object as text/plain: with GIT_CURL_VERBOSE it would
            // copy a text body to stderr in full.
            byte[] content =
            [
                0, .. RandomNumberGenerator.GetBytes(511),
                .. Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat(marker, (3 * 1024 * 1024) / marker.Length))),
            ];
            var oid = Convert.ToHexStringLower(SHA256.HashData(content));
            await LfsGit.InitLfsRepositoryAsync(gitOps, workDir, owner.Username).ConfigureAwait(false);
            await File.WriteAllBytesAsync(Path.Combine(workDir, "marker.bin"), content).ConfigureAwait(false);
            await gitOps.CommitPathsAsync(workDir, "add an lfs object with a plaintext marker").ConfigureAwait(false);
            await gitOps.AddRemoteAsync(workDir, "origin", await LfsGit.PatRemoteAsync(Context, owner, repository).ConfigureAwait(false))
                .ConfigureAwait(false);
            var push = await LfsGit.PushAsync(gitOps, workDir, "main").ConfigureAwait(false);
            Assert.True(push.Succeeded, LfsGit.WithoutTrace(push.StdErr));

            Transcript.Describe("Both encrypted replicas receive a copy (one before verify answers, one after)");
            var relativePath = LfsStorageDisk.RelativePath(repository.RepositoryId, oid);
            foreach (var node in encrypted)
            {
                await LfsStorageDisk.WaitForFileAsync(node, $"{LfsStorageDisk.EncryptedLfsRoot}/{relativePath}.aead", CopyDeadline).ConfigureAwait(false);
            }

            Transcript.Describe("A read replica on another node receives the plaintext object");
            var primary = replicas.Single(replica => replica.Role == "Primary").NodeId;
            foreach (var node in replicas.Where(replica => replica.Role == "ReadReplica" && replica.NodeId != primary))
            {
                await LfsStorageDisk.WaitForFileAsync(node.NodeId, $"{LfsStorageDisk.PlaintextLfsRoot}/{relativePath}", CopyDeadline).ConfigureAwait(false);
            }

            Transcript.Describe("No encrypted replica's disk contains the plaintext marker");
            foreach (var node in encrypted)
            {
                var grep = await LfsStorageDisk.ExecAsync(node, "grep", "-rlF", marker, "/var/lib/opengitbase", "/srv").ConfigureAwait(false);
                Assert.True(grep.ExitCode == 1, $"{node}: grep exit {grep.ExitCode}, matches: {grep.StdOut} {grep.StdErr}");
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
