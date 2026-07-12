using OpenGitBase.Cli.Api.Models;

namespace OpenGitBase.Cli.Api.Models;

public sealed class DiscussionModel
{
    public Guid Id { get; set; }

    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Body { get; set; }

    public DiscussionStatus Status { get; set; }

    public string? CreatorUsername { get; set; }

    public string? AssigneeUsername { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public IReadOnlyList<RepositoryTagModel> Tags { get; set; } = [];

    public IReadOnlyList<DiscussionCommentModel>? Comments { get; set; }
}
