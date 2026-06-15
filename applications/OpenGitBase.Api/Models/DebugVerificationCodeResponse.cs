namespace OpenGitBase.Api.Models;

public class DebugVerificationCodeResponse
{
    public required string Code { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
