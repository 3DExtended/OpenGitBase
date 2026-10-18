namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <param name="TotpConfigured">An authenticator app is confirmed; a started but unconfirmed setup does not count.</param>
/// <param name="HasPassword">False for Google/Apple-only accounts, which re-authenticate differently.</param>
public sealed record AccountSecurityStatus(bool TwoFactorEnabled, bool TotpConfigured, bool HasPassword);
