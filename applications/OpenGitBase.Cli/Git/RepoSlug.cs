namespace OpenGitBase.Cli.Git;

public sealed class RepoSlug
{
    public required string Owner { get; init; }

    public required string Slug { get; init; }

    public override string ToString() => $"{Owner}/{Slug}";
}
