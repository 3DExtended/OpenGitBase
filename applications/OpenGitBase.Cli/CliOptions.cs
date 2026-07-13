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
            Description = "Title",
            Required = true,
        };

    public static Option<string?> MrTitleOption { get; } =
        new("--title")
        {
            Description = "Updated title",
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
            Description = "Filter by status: open, engaged, resolved, dismissed (issue) or draft, open, approved, merged, closed (mr)",
        };

    public static Option<string?> MrStatusOption { get; } =
        new("--status")
        {
            Description = "Filter by status: draft, open, approved, merged, closed",
        };

    public static Option<string?> HeadOption { get; } =
        new("--head")
        {
            Description = "Source branch name (defaults to current git branch)",
        };

    public static Option<string?> BaseOption { get; } =
        new("--base")
        {
            Description = "Target branch name (defaults to repository default branch)",
        };

    public static Option<bool> DraftOption { get; } =
        new("--draft")
        {
            Description = "Create as draft merge request",
        };

    public static Option<bool> CommitsOption { get; } =
        new("--commits")
        {
            Description = "Include commit list in view output",
        };

    public static Option<string?> StrategyOption { get; } =
        new("--strategy")
        {
            Description = "Merge strategy: merge-commit, squash, fast-forward",
        };

    public static Option<bool> DeleteBranchOption { get; } =
        new("--delete-branch")
        {
            Description = "Delete source branch after merge",
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

    public static Argument<int> MergeRequestNumberArgument { get; } =
        new("NUMBER")
        {
            Description = "Merge request number",
        };
}
