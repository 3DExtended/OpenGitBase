namespace OpenGitBase.Cli.Api.Models;

public sealed class DiscussionCommentModel
{
    public Guid Id { get; set; }

    public string? AuthorUsername { get; set; }

    public string BodyMarkdown { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
