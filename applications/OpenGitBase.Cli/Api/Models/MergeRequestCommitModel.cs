namespace OpenGitBase.Cli.Api.Models;

public sealed class MergeRequestCommitModel
{
    public string Sha { get; set; } = string.Empty;

    public string ShortSha { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;

    public string AuthoredAt { get; set; } = string.Empty;
}
