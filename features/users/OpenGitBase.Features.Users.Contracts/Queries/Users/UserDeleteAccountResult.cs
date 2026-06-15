﻿namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserDeleteAccountResult
{
    public bool Success { get; set; }

    public List<UserDeleteAccountBlocker> Blockers { get; set; } = [];
}
