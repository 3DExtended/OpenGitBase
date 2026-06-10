using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserPasswordResetQuery : IQuery<Unit, UserPasswordResetQuery>
{
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string ResetCode { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}
