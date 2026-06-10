using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserCreateQuery : IQuery<UserId, UserCreateQuery>
{
    public required User ModelToCreate { get; set; }

    public UserCredentials? UserCredentials { get; set; }
}
