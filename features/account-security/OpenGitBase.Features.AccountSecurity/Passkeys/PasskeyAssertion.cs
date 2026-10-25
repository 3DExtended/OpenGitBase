namespace OpenGitBase.Features.AccountSecurity.Passkeys;

/// <param name="UserId">The owner of the passkey that signed; only set when verified.</param>
internal sealed record PasskeyAssertion(PasskeyAssertionOutcome Outcome, Guid? UserId = null)
{
    public static PasskeyAssertion Rejected { get; } = new(PasskeyAssertionOutcome.Rejected);
}
