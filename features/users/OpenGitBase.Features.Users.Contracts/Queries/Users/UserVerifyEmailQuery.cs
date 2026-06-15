﻿using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserVerifyEmailQuery : IQuery<Unit, UserVerifyEmailQuery>
{
    public string Username { get; set; } = string.Empty;

    public string VerificationToken { get; set; } = string.Empty;
}
