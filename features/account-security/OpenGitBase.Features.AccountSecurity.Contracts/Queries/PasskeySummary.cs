namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <param name="IsBackedUp">The credential is synced, for example by iCloud Keychain or Google Password Manager.</param>
public sealed record PasskeySummary(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    bool IsBackedUp
);
