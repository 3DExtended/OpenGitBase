namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <param name="RecoveryCodes">The new recovery codes in plain text, shown once; only set when enabled.</param>
public sealed record PasskeyTwoFactorActivation(
    PasskeyTwoFactorActivationStatus Status,
    IReadOnlyList<string>? RecoveryCodes = null
);
