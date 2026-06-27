﻿namespace OpenGitBase.Api.Services;

public sealed class GitPushEnforcementResult
{
    public bool Allowed { get; init; }

    public string? Reason { get; init; }

    public static GitPushEnforcementResult Allow() => new() { Allowed = true };

    public static GitPushEnforcementResult Deny(string reason) =>
        new()
        {
            Allowed = false,
            Reason = reason,
        };
}
