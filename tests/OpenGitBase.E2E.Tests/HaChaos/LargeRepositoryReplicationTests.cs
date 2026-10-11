using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.HaChaos;

/// <summary>
/// #413: a repository whose bundle is larger than Kestrel's 30 MB request-body limit still
/// replicates on every push. The post-receive hook used to bundle the whole repository, encrypt it
/// and POST it hex-encoded (twice its size) to the API's /replicate-artifact, which answered 413 for
/// any bundle above ~15 MB; the push landed on the primary only and printed a Python traceback. Now
/// the hook makes one quorum-replicate call per push and the API builds the encrypted artifact on
/// the primary and uploads it to the encrypted replicas itself.
/// <para>
/// Two pushes of 16 MiB of random (incompressible) data each: the first already made a 32 MB relay
/// body, the second makes a bundle above 30 MB even as raw bytes. Each push stays below the
/// dispatcher's own 30 MB push limit.
/// </para>
/// </summary>
[Collection("Compose")]
[Trait("Category", "HaChaos")]
[Trait("Tag", "FullHa")]
[Trait("RequiresCompose", "true")]
[E2eTier(7)]
public class LargeRepositoryReplicationTests : E2eTestBase
{
    private const int FilesPerPush = 4;
    private const int BytesPerFile = 4 * 1024 * 1024;
    private const long KestrelDefaultMaxRequestBodyBytes = 30_000_000;
    private const int AeadOverheadBytes = 12 + 16;
    private const string ArtifactRoot = "/var/lib/opengitbase/artifacts";
    private static readonly TimeSpan ReplicationDeadline = TimeSpan.FromSeconds(120);
    private static readonly Regex Http413 = new(@"\b413\b", RegexOptions.Compiled);

    [RequiresFullHaFact]
    public async Task PushesPastTheOldRelayLimitReachEveryEncryptedReplica()
    {
        BeginScenario();
        Context.Normalizer.RegisterToken("REPO_ROOT", E2eEnvironment.RepoRoot);
        var identity = new IdentityFixture(Context, Transcript);
        var repositories = new RepositoryFixture(Transcript, Context.Normalizer);
        var pats = new PatFixture(Transcript, Context.Normalizer);
        var git = new GitOperations(Transcript);
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-ha-large-{Context.RunSuffix}");

        try
        {
            var owner = await identity.RegisterUserAsync($"ha-large-owner-{Context.RunSuffix}").ConfigureAwait(false);
            var admin = await identity.LoginPlatformAdminAsync().ConfigureAwait(false);
            var repoSlug = $"ha-large-{Context.RunSuffix}";
            var repository = await repositories.CreateAsync(owner, repoSlug, "HA large replication", isPrivate: true)
                .ConfigureAwait(false);
            await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);

            Transcript.Describe("A new repository is placed RF4: primary plus two encrypted replicas, nothing committed yet");
            var initial = await GetReplicationStatusAsync(admin, repository.RepositoryId).ConfigureAwait(false);
            Assert.Equal("Rf4Healthy", initial.ReplicationState);
            Assert.Equal(0, initial.PrimaryWatermark);
            Assert.Equal(2, initial.EncryptedReplicas.Count);
            await Baselines.CaptureSideChannelAsync("created", "replication", initial.ToCreatedBaseline()).ConfigureAwait(false);

            Transcript.Describe("The primary's post-receive hook replicates once per push; the repository's storage nodes trust git-owned repositories");
            var hooks = await ReadHookStateAsync(initial, repository.RepositoryId).ConfigureAwait(false);
            Assert.Equal(
                $"#!/usr/bin/env bash\nexec /usr/local/bin/storage-quorum-replicate.sh /srv/git/{repository.RepositoryId}.git --post-receive",
                hooks.PostReceive);
            Assert.All(hooks.SafeDirectory, node => Assert.Equal("*", node.Value));
            await Baselines.CaptureSideChannelAsync("hooks", "storage", new
            {
                hooks.PostReceive,
                SafeDirectoryPerNode = hooks.SafeDirectory.Values.ToList(),
            }).ConfigureAwait(false);

            var pat = await pats.CreateWritePatAsync(owner, owner.Username, repoSlug).ConfigureAwait(false);
            await git.InitRepositoryAsync(workDir, "main", owner.Username, $"{owner.Username}@example.com").ConfigureAwait(false);
            await git.AddRemoteAsync(workDir, "origin", pat.RemoteUrl).ConfigureAwait(false);

            var previous = initial;
            for (var push = 1; push <= 2; push++)
            {
                Transcript.Describe($"Push {push}: 16 MiB of random data, then every encrypted replica holds the push's artifact");
                WriteRandomFiles(workDir, push);
                await git.CommitPathsAsync(workDir, $"random data {push}").ConfigureAwait(false);
                var result = await git.TryPushAsync(workDir, "origin", "main").ConfigureAwait(false);
                AssertCleanPush(result);
                await Baselines.CaptureApiAsync($"push-{push}", new HttpCapture
                {
                    StatusCode = result.ExitCode,
                    Body = result.StdErr,
                    Method = "GIT",
                    Url = "push",
                }).ConfigureAwait(false);

                var replicated = await WaitForReplicationAsync(admin, repository.RepositoryId, previous.PrimaryWatermark + 1)
                    .ConfigureAwait(false);
                Assert.Equal(previous.PrimaryWatermark + 1, replicated.PrimaryWatermark);
                Assert.Equal(replicated.PrimaryWatermark, replicated.Primary.AppliedWatermark);
                Assert.All(replicated.EncryptedReplicas, replica => Assert.Equal(replicated.PrimaryWatermark, replica.ArtifactWatermark));
                await Baselines.CaptureSideChannelAsync($"after-push-{push}", "replication", replicated.ToReplicatedBaseline())
                    .ConfigureAwait(false);

                var artifacts = await ReadArtifactsAsync(replicated, repository.RepositoryId).ConfigureAwait(false);
                Assert.Single(artifacts.Select(artifact => artifact.BundleSha256).Distinct());
                Assert.Single(artifacts.Select(artifact => artifact.PayloadSha256).Distinct());
                Assert.All(artifacts, artifact => Assert.Equal(replicated.PrimaryWatermark, artifact.ManifestWatermark));
                var bundleBytes = artifacts[0].PayloadBytes - AeadOverheadBytes;
                Assert.True(
                    bundleBytes > push * FilesPerPush * (long)BytesPerFile,
                    $"bundle of {bundleBytes} bytes holds less than the {push * FilesPerPush * BytesPerFile} bytes pushed");
                if (push == 2)
                {
                    Assert.True(
                        bundleBytes > KestrelDefaultMaxRequestBodyBytes,
                        $"bundle of {bundleBytes} bytes does not exceed Kestrel's default body limit");
                }

                await Baselines.CaptureSideChannelAsync($"after-push-{push}", "encrypted-artifacts", new
                {
                    Watermark = replicated.PrimaryWatermark,
                    EncryptedReplicasHoldingIt = artifacts.Count,
                    SameBundleOnEveryReplica = true,
                    BundleAboveKestrelDefaultLimit = bundleBytes > KestrelDefaultMaxRequestBodyBytes,
                }).ConfigureAwait(false);
                previous = replicated;
            }

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

    private static void WriteRandomFiles(string workDir, int push)
    {
        var buffer = new byte[BytesPerFile];
        for (var file = 1; file <= FilesPerPush; file++)
        {
            RandomNumberGenerator.Fill(buffer);
            var path = Path.Combine(workDir, "data", $"push-{push}-{file}.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, buffer);
        }
    }

    private static void AssertCleanPush(GitCommandResult result)
    {
        Assert.True(result.Succeeded, result.StdErr);
        Assert.DoesNotContain("Traceback", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotMatch(Http413, result.StdErr);
        Assert.DoesNotContain("storage-quorum-replicate", result.StdErr, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reads the admin replication view without the transcript: the API lists replicas in no fixed
    /// order, so the raw body would make the baseline flaky. <see cref="ReplicationStatus"/> sorts them.
    /// </summary>
    private static async Task<ReplicationStatus> GetReplicationStatusAsync(AuthenticatedClient admin, string repositoryId)
    {
        using var response = await admin.Client.RawClient
            .GetAsync($"admin/repositories/{repositoryId}/replication")
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.True(response.IsSuccessStatusCode, $"GET replication status: {(int)response.StatusCode} {body}");
        return ReplicationStatus.Parse(body);
    }

    /// <summary>
    /// Polls without the transcript, so the number of polls does not end up in the baseline. The
    /// hook replicates before git reports the push done, so the first poll normally succeeds.
    /// </summary>
    private static async Task<ReplicationStatus> WaitForReplicationAsync(
        AuthenticatedClient admin,
        string repositoryId,
        long expectedWatermark)
    {
        var deadline = DateTimeOffset.UtcNow + ReplicationDeadline;
        ReplicationStatus? last = null;
        while (true)
        {
            using var response = await admin.Client.RawClient
                .GetAsync($"admin/repositories/{repositoryId}/replication")
                .ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                last = ReplicationStatus.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                if (last.PrimaryWatermark >= expectedWatermark
                    && last.Primary.AppliedWatermark == last.PrimaryWatermark
                    && last.EncryptedReplicas.All(replica => replica.ArtifactWatermark == last.PrimaryWatermark))
                {
                    return last;
                }
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                Assert.Fail(
                    $"Watermark {expectedWatermark} not replicated within {ReplicationDeadline.TotalSeconds:0} s; last status: "
                    + (last is null ? "none" : JsonSerializer.Serialize(last)));
            }

            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
    }

    private static async Task<HookState> ReadHookStateAsync(ReplicationStatus status, string repositoryId)
    {
        var postReceive = await ReadFromContainerAsync(
                status.Primary.NodeId,
                "cat",
                $"/srv/git/{repositoryId}.git/hooks/post-receive")
            .ConfigureAwait(false);
        var safeDirectory = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var replica in status.Replicas)
        {
            safeDirectory[replica.NodeId] = (await ReadFromContainerAsync(
                    replica.NodeId,
                    "git",
                    "config",
                    "--system",
                    "--get-all",
                    "safe.directory")
                .ConfigureAwait(false)).Trim();
        }

        return new HookState(postReceive.Trim(), safeDirectory);
    }

    private static async Task<IReadOnlyList<StoredArtifact>> ReadArtifactsAsync(ReplicationStatus status, string repositoryId)
    {
        var artifacts = new List<StoredArtifact>();
        foreach (var replica in status.EncryptedReplicas.OrderBy(replica => replica.NodeId, StringComparer.Ordinal))
        {
            var directory = $"{ArtifactRoot}/{repositoryId}/{status.PrimaryWatermark}";
            var manifestJson = await ReadFromContainerAsync(replica.NodeId, "cat", $"{directory}/manifest.json")
                .ConfigureAwait(false);
            var payload = (await ReadFromContainerAsync(
                    replica.NodeId,
                    "sh",
                    "-c",
                    $"stat -c %s {directory}/bundle.aead && sha256sum {directory}/bundle.aead")
                .ConfigureAwait(false)).Split((char[])['\n', ' '], StringSplitOptions.RemoveEmptyEntries);
            using var manifest = JsonDocument.Parse(manifestJson);
            artifacts.Add(new StoredArtifact(
                replica.NodeId,
                manifest.RootElement.GetProperty("watermark").GetInt64(),
                manifest.RootElement.GetProperty("bundleSha256").GetString() ?? string.Empty,
                long.Parse(payload[0], System.Globalization.CultureInfo.InvariantCulture),
                payload[1]));
        }

        return artifacts;
    }

    /// <summary>Read-only <c>docker exec</c> against the stack's container for a storage node.</summary>
    private static async Task<string> ReadFromContainerAsync(string nodeId, params string[] command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(E2eEnvironment.ContainerName(nodeId));
        foreach (var argument in command)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start docker.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"docker exec {E2eEnvironment.ContainerName(nodeId)} {string.Join(' ', command)} failed: {await stderr.ConfigureAwait(false)}");
        }

        return await stdout.ConfigureAwait(false);
    }

    private sealed record HookState(string PostReceive, SortedDictionary<string, string> SafeDirectory);

    private sealed record StoredArtifact(
        string NodeId,
        long ManifestWatermark,
        string BundleSha256,
        long PayloadBytes,
        string PayloadSha256);

    private sealed record ReplicaStatus(
        string NodeId,
        string Role,
        long AppliedWatermark,
        long? ArtifactWatermark,
        bool IsInSync);

    private sealed record ReplicationStatus(
        string ReplicationState,
        long PrimaryWatermark,
        IReadOnlyList<ReplicaStatus> Replicas)
    {
        public ReplicaStatus Primary => Replicas.Single(replica => replica.Role == "Primary");

        public IReadOnlyList<ReplicaStatus> EncryptedReplicas =>
            Replicas.Where(replica => replica.Role == "EncryptedReplica").ToList();

        public static ReplicationStatus Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var replicas = root.GetProperty("replicas")
                .EnumerateArray()
                .Select(replica => new ReplicaStatus(
                    replica.GetProperty("nodeId").GetString() ?? string.Empty,
                    replica.GetProperty("role").GetString() ?? string.Empty,
                    replica.GetProperty("appliedWatermark").GetInt64(),
                    replica.GetProperty("artifactWatermark").ValueKind == JsonValueKind.Null
                        ? null
                        : replica.GetProperty("artifactWatermark").GetInt64(),
                    replica.GetProperty("isInSync").GetBoolean()))
                .OrderBy(replica => replica.Role, StringComparer.Ordinal)
                .ThenBy(replica => replica.NodeId, StringComparer.Ordinal)
                .ToList();
            return new ReplicationStatus(
                root.GetProperty("replicationState").GetString() ?? string.Empty,
                root.GetProperty("primaryWatermark").GetInt64(),
                replicas);
        }

        /// <summary>
        /// Placement and timing free: which spare nodes hold the encrypted replicas depends on free
        /// bytes at create time, and whether a background pass has already stamped an empty
        /// repository's encrypted replicas with ArtifactWatermark 0 (instead of none) on its timing.
        /// </summary>
        public object ToCreatedBaseline() => new
        {
            ReplicationState,
            PrimaryWatermark,
            PrimaryAppliedWatermark = Primary.AppliedWatermark,
            EncryptedReplicas = EncryptedReplicas.Count,
        };

        public object ToReplicatedBaseline() => new
        {
            ReplicationState,
            PrimaryWatermark,
            PrimaryAppliedWatermark = Primary.AppliedWatermark,
            EncryptedReplicas = EncryptedReplicas.Count,
            EncryptedReplicasAtPrimaryWatermark = EncryptedReplicas.Count(replica =>
                replica.ArtifactWatermark == PrimaryWatermark && replica.IsInSync),
        };
    }
}
