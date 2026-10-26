namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>The account changes and sign-in incidents a user is told about by email.</summary>
public enum SecurityNotification
{
    TwoFactorEnabled = 1,
    TwoFactorDisabled = 2,
    PasskeyAdded = 3,
    RecoveryCodeUsed = 4,
    SecondFactorLockedOut = 5,
    TwoFactorReset = 6,
}
