namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <param name="TwoFactorNewlyEnabled">
/// True when this confirmation turned two-factor sign-in on, which also revoked the user's sessions.
/// </param>
/// <param name="RecoveryCodes">
/// The first set of recovery codes in plain text; only set when this confirmation turned 2FA on.
/// </param>
public sealed record TotpConfirmation(
    TotpConfirmationStatus Status,
    bool TwoFactorNewlyEnabled = false,
    IReadOnlyList<string>? RecoveryCodes = null
);
