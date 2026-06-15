﻿namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserDeleteAccountBlocker
{
    public string Type { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;
}
