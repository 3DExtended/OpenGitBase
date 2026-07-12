namespace OpenGitBase.Pipeline;

public static class CiVariableComposer
{
    public static string BuildProjectPath(string ownerSlug, string repositorySlug) =>
        $"{ownerSlug}/{repositorySlug}";

    public static string BuildProjectPathSlug(string ownerSlug, string repositorySlug) =>
        $"{Slugify(ownerSlug)}-{Slugify(repositorySlug)}";

    private static string Slugify(string value) =>
        value.Trim().ToLowerInvariant().Replace("/", "-", StringComparison.Ordinal);
}
