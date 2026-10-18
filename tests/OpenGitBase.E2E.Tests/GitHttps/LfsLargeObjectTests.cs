using System.Net;
using System.Security.Cryptography;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.GitHttps;

/// <summary>
/// An LFS object above 100 MB end to end (lfs-09): it takes every streaming path without buffering. That covers the
/// dispatcher upload, the verify that encrypts it for the encrypted replicas, the plaintext copy, the clone download
/// and the web raw download through the API.
/// </summary>
[Collection("Compose")]
[Trait("Category", "GitHttps")]
[Trait("RequiresCompose", "true")]
[E2eTier(2)]
public class LfsLargeObjectTests : E2eTestBase
{
    private const string FileName = "large.bin";
    private const int ChunkBytes = 1024 * 1024;
    private const int SizeInChunks = 120;
    private const int AeadHeaderBytes = 29;
    private const int AeadTagBytes = 16;
    private static readonly TimeSpan CopyDeadline = TimeSpan.FromMinutes(10);

    [RequiresComposeFact]
    public async Task AnObjectAboveOneHundredMegabytesRoundTripsByteIdentical()
    {
        BeginScenario();
        var gitOps = new GitOperations(Transcript);
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-lfs-large-{Context.RunSuffix}");
        var cloneDir = Path.Combine(Path.GetTempPath(), $"e2e-lfs-large-clone-{Context.RunSuffix}");

        try
        {
            await LfsGit.RequireGitLfsAsync(gitOps).ConfigureAwait(false);

            Transcript.Describe("Owner pushes a 120 MiB LFS object");
            var identity = new IdentityFixture(Context, Transcript);
            var owner = await identity.RegisterUserAsync($"lfs-large-{Context.RunSuffix}").ConfigureAwait(false);
            var admin = await identity.LoginPlatformAdminAsync().ConfigureAwait(false);
            var repository = await new RepositoryFixture(Transcript, Context.Normalizer)
                .CreateAsync(owner, "lfs-large-repo", "LFS Large Repo", isPrivate: true)
                .ConfigureAwait(false);
            await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);

            await LfsGit.InitLfsRepositoryAsync(gitOps, workDir, owner.Username).ConfigureAwait(false);
            var (oid, size) = await WriteRandomFileAsync(Path.Combine(workDir, FileName)).ConfigureAwait(false);
            await gitOps.CommitPathsAsync(workDir, "add a large lfs object").ConfigureAwait(false);
            var remote = await LfsGit.PatRemoteAsync(Context, owner, repository).ConfigureAwait(false);
            await gitOps.AddRemoteAsync(workDir, "origin", remote).ConfigureAwait(false);
            var push = await LfsGit.PushAsync(gitOps, workDir, "main").ConfigureAwait(false);
            Assert.True(push.Succeeded, LfsGit.WithoutTrace(push.StdErr));

            Transcript.Describe("Every replica gets its copy; each encrypted copy has the segmented AEAD size");
            var relativePath = LfsStorageDisk.RelativePath(repository.RepositoryId, oid);
            var encryptedSize = AeadHeaderBytes + size + ((size + ChunkBytes - 1) / ChunkBytes * AeadTagBytes);
            foreach (var (nodeId, role) in await LfsStorageDisk.GetReplicasAsync(admin, repository.RepositoryId).ConfigureAwait(false))
            {
                var encrypted = role == "EncryptedReplica";
                var copy = encrypted
                    ? $"{LfsStorageDisk.EncryptedLfsRoot}/{relativePath}.aead"
                    : $"{LfsStorageDisk.PlaintextLfsRoot}/{relativePath}";
                await LfsStorageDisk.WaitForFileAsync(nodeId, copy, CopyDeadline).ConfigureAwait(false);
                var stat = await LfsStorageDisk.ExecAsync(nodeId, "stat", "-c", "%s", copy).ConfigureAwait(false);
                Assert.True(stat.ExitCode == 0, stat.StdErr);
                var stored = long.Parse(stat.StdOut.Trim(), System.Globalization.CultureInfo.InvariantCulture);
                Assert.Equal(encrypted ? encryptedSize : size, stored);
            }

            Transcript.Describe("A clone smudges the object back byte-identical");
            var clone = await LfsGit.CloneAsync(gitOps, remote, cloneDir).ConfigureAwait(false);
            Assert.True(clone.Succeeded, LfsGit.WithoutTrace(clone.StdErr));
            Assert.Equal(oid, await HashFileAsync(Path.Combine(cloneDir, FileName)).ConfigureAwait(false));

            Transcript.Describe("The web raw download streams the object through the API with its length");
            var query = $"refName=main&path={Uri.EscapeDataString(FileName)}";
            var rawUrl = $"repository/by-slug/{owner.Username}/{repository.Slug}/content/blob/raw?{query}";
            using var raw = await owner.Client.RawClient
                .GetAsync(rawUrl, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            Assert.Equal(HttpStatusCode.OK, raw.StatusCode);
            Assert.Equal(size, raw.Content.Headers.ContentLength);
            Assert.Equal("application/octet-stream", raw.Content.Headers.ContentType?.MediaType);
            await using var body = await raw.Content.ReadAsStreamAsync().ConfigureAwait(false);
            Assert.Equal(oid, Convert.ToHexStringLower(await SHA256.HashDataAsync(body).ConfigureAwait(false)));
        }
        finally
        {
            foreach (var directory in new[] { workDir, cloneDir })
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }

    /// <summary>
    /// Random bytes written chunk by chunk, so the test never holds the object in memory. The leading zero byte keeps
    /// git-lfs from treating it as text and copying it to stderr under <c>GIT_CURL_VERBOSE</c>.
    /// </summary>
    private static async Task<(string Oid, long Size)> WriteRandomFileAsync(string path)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var file = File.Create(path);
        var chunk = new byte[ChunkBytes];
        for (var index = 0; index < SizeInChunks; index++)
        {
            RandomNumberGenerator.Fill(chunk);
            if (index == 0)
            {
                chunk[0] = 0;
            }

            hash.AppendData(chunk);
            await file.WriteAsync(chunk).ConfigureAwait(false);
        }

        return (Convert.ToHexStringLower(hash.GetHashAndReset()), (long)SizeInChunks * ChunkBytes);
    }

    private static async Task<string> HashFileAsync(string path)
    {
        await using var file = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(file).ConfigureAwait(false));
    }
}
