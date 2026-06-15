﻿using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserDeleteAccountQuery : IQuery<UserDeleteAccountResult, UserDeleteAccountQuery>
{
    public UserId UserId { get; set; } = null!;

    public string Password { get; set; } = string.Empty;
}
