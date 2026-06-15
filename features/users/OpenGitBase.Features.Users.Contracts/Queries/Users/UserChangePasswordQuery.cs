﻿using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserChangePasswordQuery : IQuery<Unit, UserChangePasswordQuery>
{
    public UserId UserId { get; set; } = null!;

    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}
