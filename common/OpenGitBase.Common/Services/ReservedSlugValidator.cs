namespace OpenGitBase.Common.Services;

public static class ReservedSlugValidator
{
    // Keep in sync with applications/opengitbase-web/app/utils/slug-validation.ts
    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "__visual__",
        "admin",
        "api",
        "explore",
        "forgot-password",
        "gate",
        "health",
        "invite",
        "opengitbase",
        "orgs",
        "pitch",
        "register",
        "repos",
        "reset-password",
        "settings",
        "sign-in",
        "sign-out",
        "sign-up",
        "status",
        "swagger",
        "verify-email",
    };

    public static bool IsReserved(string slug) =>
        !string.IsNullOrWhiteSpace(slug) && ReservedSlugs.Contains(slug.Trim());
}
