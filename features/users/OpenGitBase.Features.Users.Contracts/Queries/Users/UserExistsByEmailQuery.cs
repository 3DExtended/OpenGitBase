using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserExistsByEmailQuery : IQuery<bool, UserExistsByEmailQuery>
{
    public string Email { get; set; } = string.Empty;
}
