namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <param name="OptionsJson">The request options; only set when issued.</param>
public sealed record TwoFactorPasskeyOptions(TwoFactorPasskeyOptionsStatus Status, string? OptionsJson = null);
