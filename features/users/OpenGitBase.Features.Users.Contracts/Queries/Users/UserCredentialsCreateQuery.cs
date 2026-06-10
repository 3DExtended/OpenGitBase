using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserCredentialsCreateQuery
    : CreateQuery<UserCredentials, UserCredentialsId, Guid, UserCredentialsCreateQuery>;
