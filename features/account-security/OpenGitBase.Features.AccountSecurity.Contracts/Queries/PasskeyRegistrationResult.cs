namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <param name="Passkey">Only set when <see cref="PasskeyRegistrationStatus.Registered"/>.</param>
public sealed record PasskeyRegistrationResult(PasskeyRegistrationStatus Status, PasskeySummary? Passkey = null);
