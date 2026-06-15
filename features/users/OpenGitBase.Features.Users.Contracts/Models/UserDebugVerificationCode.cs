namespace OpenGitBase.Features.Users.Contracts.Models;

public class UserDebugVerificationCode
{
    public required string Code { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }
}
