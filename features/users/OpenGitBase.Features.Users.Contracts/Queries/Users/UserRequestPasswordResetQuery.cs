using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserRequestPasswordResetQuery : IQuery<Unit, UserRequestPasswordResetQuery>
{
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}
