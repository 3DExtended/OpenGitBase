namespace OpenGitBase.Api.Services;

/// <summary>What an encrypted LFS copy is bound to: the Repository Key (and its version), the repository and the oid.</summary>
public sealed record LfsCipherContext(byte[] Key, int KeyVersion, Guid RepositoryId, string Oid);
