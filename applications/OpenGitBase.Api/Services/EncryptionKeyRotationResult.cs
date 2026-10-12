namespace OpenGitBase.Api.Services;

public sealed record EncryptionKeyRotationResult(int ReprotectedValues, int RecomputedLookupHashes);
