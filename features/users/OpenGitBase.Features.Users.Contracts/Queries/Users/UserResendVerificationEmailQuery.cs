﻿using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserResendVerificationEmailQuery : IQuery<Unit, UserResendVerificationEmailQuery>
{
    public UserId UserId { get; set; } = null!;
}
