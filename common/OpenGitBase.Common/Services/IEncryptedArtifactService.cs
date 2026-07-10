using OpenGitBase.Common.Storage;

namespace OpenGitBase.Common.Services;

public interface IEncryptedArtifactService
{
    string BuildAssociatedData(Guid repositoryId, long watermark, long epoch);

    string ComputeBundleSha256(byte[] bundlePlaintext);

    EncryptedArtifactEnvelope EncryptBundle(
        byte[] bundlePlaintext,
        byte[] repositoryKey,
        EncryptedArtifactManifest manifest,
        Guid repositoryId
    );

    byte[] DecryptBundle(
        byte[] ciphertext,
        byte[] repositoryKey,
        Guid repositoryId,
        long watermark,
        long epoch
    );

    void VerifyEnvelope(
        EncryptedArtifactEnvelope envelope,
        byte[] repositoryKey,
        Guid repositoryId
    );
}
