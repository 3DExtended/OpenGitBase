using System.CommandLine;

namespace OpenGitBase.Cli;

public static class CliOptions
{
    public static Option<string?> HostnameOption { get; } =
        new("--hostname")
        {
            Description = "OpenGitBase host URL (default: https://www.opengitbase.com/)",
        };

    public static Option<bool> JsonOption { get; } =
        new("--json")
        {
            Description = "Output JSON",
        };

    public static Option<string?> RepoOption { get; } =
        new("-R", "--repo")
        {
            Description = "Repository owner/slug (overrides git remote inference)",
        };

    public static Option<string> TitleOption { get; } =
        new("--title")
        {
            Description = "Issue title",
            Required = true,
        };

    public static Option<string?> BodyOption { get; } =
        new("--body")
        {
            Description = "Markdown body text",
        };

    public static Option<FileInfo?> BodyFileOption { get; } =
        new("--body-file")
        {
            Description = "Path to a file containing markdown body text",
        };

    public static Option<string?> StatusOption { get; } =
        new("--status")
        {
            Description = "Filter by status: open, engaged, resolved, dismissed",
        };

    public static Option<string?> ReasonOption { get; } =
        new("--reason")
        {
            Description = "Close reason: dismissed (default resolves the issue)",
        };

    public static Argument<int> IssueNumberArgument { get; } =
        new("NUMBER")
        {
            Description = "Issue number",
        };
}
