using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>Verifies the browser's attestation for an open registration challenge and stores the passkey.</summary>
public class CompletePasskeyRegistrationQuery : IQuery<PasskeyRegistrationResult, CompletePasskeyRegistrationQuery>
{
    public UserId UserId { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>The credential as <c>PublicKeyCredential.toJSON()</c> returns it.</summary>
    public string AttestationResponseJson { get; set; } = string.Empty;
}
