namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <param name="Codes">The plaintext codes, formatted <c>xxxxx-xxxxx</c>; they are never returned again.</param>
public sealed record RecoveryCodeSet(IReadOnlyList<string> Codes);
