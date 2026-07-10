namespace OpenGitBase.Common.Storage;

public sealed record EncryptedArtifactManifest(
    long Epoch,
    long Watermark,
    string BundleSha256,
    int KeyVersion
);
