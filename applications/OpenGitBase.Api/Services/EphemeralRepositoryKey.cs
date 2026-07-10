namespace OpenGitBase.Api.Services;

public sealed record EphemeralRepositoryKey(byte[] KeyMaterial, int KeyVersion);
