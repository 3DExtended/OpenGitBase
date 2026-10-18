namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

public enum TotpConfirmationStatus
{
    Confirmed = 1,
    InvalidCode = 2,
    NoPendingSetup = 3,
}
