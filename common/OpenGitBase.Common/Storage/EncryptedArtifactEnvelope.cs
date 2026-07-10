namespace OpenGitBase.Common.Storage;

public sealed record EncryptedArtifactEnvelope(
    EncryptedArtifactManifest Manifest,
    byte[] Ciphertext
);
