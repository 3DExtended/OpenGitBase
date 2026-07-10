using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Storage;

namespace OpenGitBase.Common.Tests.Services;

public class EncryptedArtifactServiceTests
{
    private readonly EncryptedArtifactService _service = new();
    private readonly byte[] _repositoryKey = Convert.FromBase64String(
        Convert.ToBase64String(new byte[32])
    );

    [Fact]
    public void EncryptDecrypt_RoundTripsBundleBytes()
    {
        var repositoryId = Guid.NewGuid();
        var bundle = "git bundle plaintext"u8.ToArray();
        var manifest = CreateManifest(bundle, watermark: 1, epoch: 2);

        var envelope = _service.EncryptBundle(bundle, _repositoryKey, manifest, repositoryId);
        var decrypted = _service.DecryptBundle(
            envelope.Ciphertext,
            _repositoryKey,
            repositoryId,
            manifest.Watermark,
            manifest.Epoch
        );

        Assert.Equal(bundle, decrypted);
    }

    [Fact]
    public void EncryptBundle_WhenManifestHashMismatch_Throws()
    {
        var bundle = "bundle"u8.ToArray();
        var manifest = new EncryptedArtifactManifest(1, 1, "deadbeef", 1);

        Assert.Throws<InvalidOperationException>(() =>
            _service.EncryptBundle(bundle, _repositoryKey, manifest, Guid.NewGuid())
        );
    }

    [Fact]
    public void DecryptBundle_WhenTampered_Throws()
    {
        var repositoryId = Guid.NewGuid();
        var bundle = "tamper test"u8.ToArray();
        var manifest = CreateManifest(bundle, watermark: 3, epoch: 4);
        var envelope = _service.EncryptBundle(bundle, _repositoryKey, manifest, repositoryId);
        envelope.Ciphertext[^1] ^= 0xFF;

        Assert.ThrowsAny<Exception>(() =>
            _service.DecryptBundle(
                envelope.Ciphertext,
                _repositoryKey,
                repositoryId,
                manifest.Watermark,
                manifest.Epoch
            )
        );
    }

    [Fact]
    public void VerifyEnvelope_WhenManifestHashMismatchAfterDecrypt_Throws()
    {
        var repositoryId = Guid.NewGuid();
        var bundle = "verify"u8.ToArray();
        var manifest = CreateManifest(bundle, watermark: 5, epoch: 6);
        var envelope = _service.EncryptBundle(bundle, _repositoryKey, manifest, repositoryId);
        var badManifest = manifest with { BundleSha256 = "00" };
        var badEnvelope = envelope with { Manifest = badManifest };

        Assert.ThrowsAny<Exception>(() =>
            _service.VerifyEnvelope(badEnvelope, _repositoryKey, repositoryId)
        );
    }

    private EncryptedArtifactManifest CreateManifest(byte[] bundle, long watermark, long epoch) =>
        new(epoch, watermark, _service.ComputeBundleSha256(bundle), 1);
}
