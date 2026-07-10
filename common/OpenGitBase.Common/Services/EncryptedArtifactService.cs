using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using OpenGitBase.Common.Storage;

namespace OpenGitBase.Common.Services;

public class EncryptedArtifactService : IEncryptedArtifactService
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string SerializeManifest(EncryptedArtifactManifest manifest) =>
        JsonSerializer.Serialize(manifest, ManifestJsonOptions);

    public static EncryptedArtifactManifest DeserializeManifest(string manifestJson) =>
        JsonSerializer.Deserialize<EncryptedArtifactManifest>(manifestJson, ManifestJsonOptions)
        ?? throw new InvalidOperationException("Artifact manifest JSON is invalid.");

    public string BuildAssociatedData(Guid repositoryId, long watermark, long epoch) =>
        $"{repositoryId:D}:{watermark}:{epoch}";

    public string ComputeBundleSha256(byte[] bundlePlaintext)
    {
        var hash = SHA256.HashData(bundlePlaintext);
        return Convert.ToHexString(hash);
    }

    public EncryptedArtifactEnvelope EncryptBundle(
        byte[] bundlePlaintext,
        byte[] repositoryKey,
        EncryptedArtifactManifest manifest,
        Guid repositoryId
    )
    {
        ValidateKey(repositoryKey);
        var expectedHash = ComputeBundleSha256(bundlePlaintext);
        if (!string.Equals(expectedHash, manifest.BundleSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Bundle SHA-256 does not match manifest.");
        }

        var associatedData = Encoding.UTF8.GetBytes(
            BuildAssociatedData(repositoryId, manifest.Watermark, manifest.Epoch)
        );
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[bundlePlaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(repositoryKey, tagSizeInBytes: 16);
        aes.Encrypt(nonce, bundlePlaintext, ciphertext, tag, associatedData);

        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);

        return new EncryptedArtifactEnvelope(manifest, payload);
    }

    public byte[] DecryptBundle(
        byte[] ciphertext,
        byte[] repositoryKey,
        Guid repositoryId,
        long watermark,
        long epoch
    )
    {
        ValidateKey(repositoryKey);
        var associatedData = Encoding.UTF8.GetBytes(BuildAssociatedData(repositoryId, watermark, epoch));
        var nonce = ciphertext[..12];
        var tag = ciphertext[12..28];
        var encrypted = ciphertext[28..];
        var plaintext = new byte[encrypted.Length];

        using var aes = new AesGcm(repositoryKey, tagSizeInBytes: 16);
        aes.Decrypt(nonce, encrypted, tag, plaintext, associatedData);

        return plaintext;
    }

    public void VerifyEnvelope(
        EncryptedArtifactEnvelope envelope,
        byte[] repositoryKey,
        Guid repositoryId
    )
    {
        var plaintext = DecryptBundle(
            envelope.Ciphertext,
            repositoryKey,
            repositoryId,
            envelope.Manifest.Watermark,
            envelope.Manifest.Epoch
        );
        var hash = ComputeBundleSha256(plaintext);
        if (!string.Equals(hash, envelope.Manifest.BundleSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new CryptographicException("Decrypted bundle hash does not match manifest.");
        }
    }

    private static void ValidateKey(byte[] repositoryKey)
    {
        if (repositoryKey.Length != 32)
        {
            throw new InvalidOperationException("Repository key must be 32 bytes.");
        }
    }
}
