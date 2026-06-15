﻿using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserGetEmailVerifiedQuery : IQuery<bool, UserGetEmailVerifiedQuery>
{
    public UserId UserId { get; set; } = null!;
}
