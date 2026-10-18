namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <param name="TwoFactorNewlyEnabled">
/// True when this confirmation turned two-factor sign-in on, which also revoked the user's sessions.
/// </param>
public sealed record TotpConfirmation(TotpConfirmationStatus Status, bool TwoFactorNewlyEnabled = false);
