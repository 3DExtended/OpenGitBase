namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

public sealed record PasskeyList(IReadOnlyList<PasskeySummary> Passkeys);
