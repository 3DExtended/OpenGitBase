namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <param name="LockedUntil">When codes are accepted again; only set when locked out.</param>
public sealed record ReauthenticationResult(ReauthenticationStatus Status, DateTimeOffset? LockedUntil = null);
