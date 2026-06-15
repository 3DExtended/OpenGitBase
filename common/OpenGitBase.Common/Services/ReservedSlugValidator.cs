﻿namespace OpenGitBase.Common.Services;

public static class ReservedSlugValidator
{
    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "opengitbase",
        "admin",
        "explore",
        "sign-in",
        "sign-up",
        "settings",
        "api",
        "register",
        "health",
        "swagger",
        "__visual__",
        "forgot-password",
        "reset-password",
        "verify-email",
        "sign-out",
    };

    public static bool IsReserved(string slug) =>
        !string.IsNullOrWhiteSpace(slug) && ReservedSlugs.Contains(slug.Trim());
}
