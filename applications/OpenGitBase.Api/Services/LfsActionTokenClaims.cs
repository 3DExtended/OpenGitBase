namespace OpenGitBase.Api.Services;

/// <summary>What an LFS action token allows: one operation on one object of one repository.</summary>
public sealed record LfsActionTokenClaims(
    Guid RepositoryId,
    string Oid,
    long Size,
    LfsTransferOperation Operation,
    Guid? UserId,
    DateTimeOffset ExpiresAt
);
