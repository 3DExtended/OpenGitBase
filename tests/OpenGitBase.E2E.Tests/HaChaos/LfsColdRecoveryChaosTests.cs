using System.Security.Cryptography;
using System.Text.Json;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;
using OpenGitBase.E2E.Tests.GitHttps;

namespace OpenGitBase.E2E.Tests.HaChaos;

/// <summary>
/// The durability guarantee for Git LFS under node loss (lfs-09). The plaintext node of a repository (its primary,
/// which is also its read replica in this stack) loses its LFS data and goes down. Failover cold-recovers git from
/// an encrypted replica onto a node without a copy, and the LFS object is decrypted back from the encrypted copies:
/// a clone returns it byte-identical. When the old node returns without the object, downloads fall back to the new
/// primary.
/// <para>
/// storage-1 is the primary of most repositories on the stack, and stopping it would cold-recover all of them. The
/// test keeps storage-1 out of the placement while the repository is created, so only the test repository's primary
/// goes down.
/// </para>
/// </summary>
[Collection("Compose")]
[Trait("Category", "HaChaos")]
[Trait("Tag", "FullHa")]
[Trait("RequiresCompose", "true")]
[E2eTier(7)]
public class LfsColdRecoveryChaosTests : E2eTestBase
{
    private const string BusiestNode = "storage-1";
    private static readonly TimeSpan CopyDeadline = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RecoveryDeadline = TimeSpan.FromMinutes(10);

    [RequiresFullHaFact]
    public async Task LosingThePlaintextNodeRestoresGitAndLfsFromTheEncryptedReplicas()
    {
        BeginScenario();
        var gitOps = new GitOperations(Transcript);
        var chaos = new ClusterChaos(Transcript);
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-lfs-cold-{Context.RunSuffix}");
        var cloneDir = Path.Combine(Path.GetTempPath(), $"e2e-lfs-cold-clone-{Context.RunSuffix}");
        var fallbackCloneDir = Path.Combine(Path.GetTempPath(), $"e2e-lfs-cold-fallback-{Context.RunSuffix}");
        AuthenticatedClient? admin = null;
        (string Id, long MaxBytes)? cappedNode = null;

        try
        {
            await LfsGit.RequireGitLfsAsync(gitOps).ConfigureAwait(false);
            var identity = new IdentityFixture(Context, Transcript);
            var owner = await identity.RegisterUserAsync($"lfs-cold-{Context.RunSuffix}").ConfigureAwait(false);
            admin = await identity.LoginPlatformAdminAsync().ConfigureAwait(false);

            Transcript.Describe("The repository is placed while storage-1 takes no new repositories");
            cappedNode = await GetNodeAsync(admin, BusiestNode).ConfigureAwait(false);
            await SetCapacityAsync(admin, cappedNode.Value.Id, 1).ConfigureAwait(false);
            var repository = await new RepositoryFixture(Transcript, Context.Normalizer)
                .CreateAsync(owner, "lfs-cold-repo", "LFS Cold Recovery Repo", isPrivate: true)
                .ConfigureAwait(false);
            await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);
            await SetCapacityAsync(admin, cappedNode.Value.Id, cappedNode.Value.MaxBytes).ConfigureAwait(false);
            cappedNode = null;

            var placement = await ReadReplicationAsync(admin, repository.RepositoryId).ConfigureAwait(false);
            var lostNode = placement.Primary;
            Assert.NotEqual(BusiestNode, lostNode);
            Assert.Equal(2, placement.EncryptedReplicas.Count);

            Transcript.Describe("Owner pushes an LFS object; both encrypted replicas hold it and the push's git artifact");
            byte[] content = [0, .. RandomNumberGenerator.GetBytes(3 * 1024 * 1024)];
            var oid = Convert.ToHexStringLower(SHA256.HashData(content));
            await LfsGit.InitLfsRepositoryAsync(gitOps, workDir, owner.Username).ConfigureAwait(false);
            await File.WriteAllBytesAsync(Path.Combine(workDir, "asset.bin"), content).ConfigureAwait(false);
            await gitOps.CommitPathsAsync(workDir, "add an lfs object").ConfigureAwait(false);
            var remote = await LfsGit.PatRemoteAsync(Context, owner, repository).ConfigureAwait(false);
            await gitOps.AddRemoteAsync(workDir, "origin", remote).ConfigureAwait(false);
            var push = await LfsGit.PushAsync(gitOps, workDir, "main").ConfigureAwait(false);
            Assert.True(push.Succeeded, LfsGit.WithoutTrace(push.StdErr));

            var relativePath = LfsStorageDisk.RelativePath(repository.RepositoryId, oid);
            foreach (var node in placement.EncryptedReplicas)
            {
                await LfsStorageDisk
                    .WaitForFileAsync(node, $"{LfsStorageDisk.EncryptedLfsRoot}/{relativePath}.aead", CopyDeadline)
                    .ConfigureAwait(false);
            }

            await WaitUntilAsync(
                    admin,
                    repository.RepositoryId,
                    status => status.PrimaryWatermark > 0
                        && status.EncryptedArtifactWatermarks.All(watermark => watermark >= status.PrimaryWatermark),
                    CopyDeadline,
                    "the encrypted replicas to hold the push's git artifact"
                )
                .ConfigureAwait(false);

            Transcript.Describe("The primary loses its LFS data and goes down");
            var directory = repository.RepositoryId.ToLowerInvariant();
            var wiped = await LfsStorageDisk
                .ExecAsync(lostNode, "rm", "-rf", $"{LfsStorageDisk.PlaintextLfsRoot}/{directory}")
                .ConfigureAwait(false);
            Assert.True(wiped.ExitCode == 0, wiped.StdErr);
            await chaos.StopServiceAsync(lostNode).ConfigureAwait(false);

            Transcript.Describe("Failover cold-recovers git onto the node without a copy; both encrypted replicas stay");
            var recovered = await WaitUntilAsync(
                    admin,
                    repository.RepositoryId,
                    status => status.Primary != lostNode && status.ReplicationState != "Recovering",
                    RecoveryDeadline,
                    "cold recovery to move the primary",
                    markStaleNodes: true
                )
                .ConfigureAwait(false);
            Assert.Equal(BusiestNode, recovered.Primary);
            Assert.Equal(placement.EncryptedReplicas.Order(), recovered.EncryptedReplicas.Order());
            Assert.True(recovered.ReplicationEpoch > placement.ReplicationEpoch);

            Transcript.Describe("A clone returns the LFS object byte-identical; it is decrypted onto the new primary");
            var clone = await LfsGit.CloneAsync(gitOps, remote, cloneDir).ConfigureAwait(false);
            Assert.True(clone.Succeeded, LfsGit.WithoutTrace(clone.StdErr));
            await AssertClonedObjectAsync(cloneDir, oid).ConfigureAwait(false);
            await LfsStorageDisk
                .WaitForFileAsync(recovered.Primary, $"{LfsStorageDisk.PlaintextLfsRoot}/{relativePath}", CopyDeadline)
                .ConfigureAwait(false);

            // The import once left a working-tree clone: git refused the push and the replication hook never ran.
            Transcript.Describe("The restored primary takes a push and replicates it to both encrypted replicas");
            await File.WriteAllTextAsync(Path.Combine(workDir, "after-recovery.txt"), "pushed to the restored primary\n")
                .ConfigureAwait(false);
            await gitOps.CommitPathsAsync(workDir, "push after cold recovery").ConfigureAwait(false);
            var pushAfterRecovery = await LfsGit.PushAsync(gitOps, workDir, "main").ConfigureAwait(false);
            Assert.True(pushAfterRecovery.Succeeded, LfsGit.WithoutTrace(pushAfterRecovery.StdErr));
            await WaitUntilAsync(
                    admin,
                    repository.RepositoryId,
                    status => status.PrimaryWatermark > recovered.PrimaryWatermark
                        && status.EncryptedArtifactWatermarks.All(watermark => watermark >= status.PrimaryWatermark),
                    CopyDeadline,
                    "the encrypted replicas to hold the artifact of the push after recovery"
                )
                .ConfigureAwait(false);

            Transcript.Describe("The old primary returns without the object; downloads fall back to the new primary");
            await chaos.StartServiceAsync(lostNode).ConfigureAwait(false);
            await LfsStorageDisk.WaitForHealthyAsync(lostNode, RecoveryDeadline).ConfigureAwait(false);
            await WaitForNodeHealthyAsync(admin, lostNode, RecoveryDeadline).ConfigureAwait(false);
            var stillMissing = await LfsStorageDisk
                .ExecAsync(lostNode, "rm", "-rf", $"{LfsStorageDisk.PlaintextLfsRoot}/{directory}")
                .ConfigureAwait(false);
            Assert.True(stillMissing.ExitCode == 0, stillMissing.StdErr);
            var fallback = await LfsGit.CloneAsync(gitOps, remote, fallbackCloneDir).ConfigureAwait(false);
            Assert.True(fallback.Succeeded, LfsGit.WithoutTrace(fallback.StdErr));
            await AssertClonedObjectAsync(fallbackCloneDir, oid).ConfigureAwait(false);
        }
        finally
        {
            if (admin is not null && cappedNode is not null)
            {
                await SetCapacityAsync(admin, cappedNode.Value.Id, cappedNode.Value.MaxBytes).ConfigureAwait(false);
            }

            await chaos.RestoreAllAsync().ConfigureAwait(false);
            foreach (var path in new[] { workDir, cloneDir, fallbackCloneDir })
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
        }
    }

    private static async Task AssertClonedObjectAsync(string cloneDir, string oid)
    {
        var cloned = await File.ReadAllBytesAsync(Path.Combine(cloneDir, "asset.bin")).ConfigureAwait(false);
        Assert.Equal(oid, Convert.ToHexStringLower(SHA256.HashData(cloned)));
    }

    private static async Task<(string Id, long MaxBytes)> GetNodeAsync(AuthenticatedClient admin, string nodeId)
    {
        using var document = await GetJsonAsync(admin, "admin/storage-nodes").ConfigureAwait(false);
        var node = document.RootElement.EnumerateArray().Single(entry => entry.GetProperty("nodeId").GetString() == nodeId);
        return (node.GetProperty("id").GetProperty("value").GetString() ?? string.Empty, node.GetProperty("maxBytes").GetInt64());
    }

    private static async Task SetCapacityAsync(AuthenticatedClient admin, string storageNodeId, long maxBytes)
    {
        var response = await admin.Client
            .SendAsync(HttpMethod.Patch, $"/admin/storage-nodes/{storageNodeId}/capacity", new { maxBytes })
            .ConfigureAwait(false);
        Assert.True(response.StatusCode == 200, $"PATCH capacity: {response.StatusCode} {response.Body}");
    }

    /// <summary>Polls without the transcript until the API lists the node as healthy again.</summary>
    private static async Task WaitForNodeHealthyAsync(AuthenticatedClient admin, string nodeId, TimeSpan deadline)
    {
        var until = DateTimeOffset.UtcNow + deadline;
        while (true)
        {
            using (var document = await GetJsonAsync(admin, "admin/storage-nodes").ConfigureAwait(false))
            {
                if (document.RootElement.EnumerateArray()
                    .Any(entry => entry.GetProperty("nodeId").GetString() == nodeId && entry.GetProperty("isHealthy").GetBoolean()))
                {
                    return;
                }
            }

            Assert.True(DateTimeOffset.UtcNow < until, $"{nodeId} was not healthy again within {deadline.TotalSeconds:0} s.");
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Polls the replication status without the transcript. With <paramref name="markStaleNodes"/> it also asks
    /// for the fleet drift report, which marks nodes that stopped sending heartbeats as unhealthy; failover only
    /// acts on nodes marked so.
    /// </summary>
    private static async Task<ReplicationStatus> WaitUntilAsync(
        AuthenticatedClient admin,
        string repositoryId,
        Func<ReplicationStatus, bool> condition,
        TimeSpan deadline,
        string what,
        bool markStaleNodes = false
    )
    {
        var until = DateTimeOffset.UtcNow + deadline;
        while (true)
        {
            if (markStaleNodes)
            {
                using var drift = await admin.Client.RawClient.GetAsync("admin/fleet/drift").ConfigureAwait(false);
            }

            var status = await ReadReplicationAsync(admin, repositoryId).ConfigureAwait(false);
            if (condition(status))
            {
                return status;
            }

            Assert.True(DateTimeOffset.UtcNow < until, $"Timed out after {deadline.TotalSeconds:0} s waiting for {what}; last: {status}.");
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
    }

    private static async Task<ReplicationStatus> ReadReplicationAsync(AuthenticatedClient admin, string repositoryId)
    {
        using var document = await GetJsonAsync(admin, $"admin/repositories/{repositoryId}/replication").ConfigureAwait(false);
        var root = document.RootElement;
        var replicas = root.GetProperty("replicas").EnumerateArray().ToList();
        var encrypted = replicas.Where(replica => replica.GetProperty("role").GetString() == "EncryptedReplica").ToList();
        return new ReplicationStatus(
            root.GetProperty("replicationState").GetString() ?? string.Empty,
            root.GetProperty("replicationEpoch").GetInt64(),
            root.GetProperty("primaryWatermark").GetInt64(),
            replicas
                .Where(replica => replica.GetProperty("role").GetString() == "Primary")
                .Select(replica => replica.GetProperty("nodeId").GetString() ?? string.Empty)
                .SingleOrDefault() ?? string.Empty,
            [.. encrypted.Select(replica => replica.GetProperty("nodeId").GetString() ?? string.Empty)],
            [.. encrypted.Select(replica => replica.TryGetProperty("artifactWatermark", out var watermark)
                && watermark.ValueKind == JsonValueKind.Number
                    ? watermark.GetInt64()
                    : 0)]
        );
    }

    private static async Task<JsonDocument> GetJsonAsync(AuthenticatedClient admin, string path)
    {
        using var response = await admin.Client.RawClient.GetAsync(path).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.True(response.IsSuccessStatusCode, $"GET {path}: {(int)response.StatusCode} {body}");
        return JsonDocument.Parse(body);
    }

    private sealed record ReplicationStatus(
        string ReplicationState,
        long ReplicationEpoch,
        long PrimaryWatermark,
        string Primary,
        IReadOnlyList<string> EncryptedReplicas,
        IReadOnlyList<long> EncryptedArtifactWatermarks
    );
}
