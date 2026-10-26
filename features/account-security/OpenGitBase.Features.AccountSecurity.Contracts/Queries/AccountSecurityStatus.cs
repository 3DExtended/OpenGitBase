namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <param name="TotpConfigured">An authenticator app is confirmed; a started but unconfirmed setup does not count.</param>
/// <param name="HasPassword">False for Google/Apple-only accounts, which re-authenticate differently.</param>
/// <param name="RecoveryCodesRemaining">Unused recovery codes; spent ones do not count.</param>
public sealed record AccountSecurityStatus(
    bool TwoFactorEnabled,
    bool TotpConfigured,
    bool HasPassword,
    int RecoveryCodesRemaining
);
