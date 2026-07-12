using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGitBase.Cli.Api.Models;

namespace OpenGitBase.Cli.Output;

public sealed class JsonOutputWriter : IOutputWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly TextWriter _output;

    public JsonOutputWriter(TextWriter output) => _output = output;

    public void WriteAuthStatus(AuthStatusOutput status) => WriteObject(status);

    public void WriteIssueCreated(DiscussionModel discussion, string url) =>
        WriteObject(new
        {
            number = discussion.Number,
            title = discussion.Title,
            status = discussion.Status,
            url,
        });

    public void WriteIssueComment(DiscussionCommentModel comment, int issueNumber) =>
        WriteObject(new
        {
            issueNumber,
            commentId = comment.Id,
            author = comment.AuthorUsername,
            createdAt = comment.CreatedAt,
        });

    public void WriteIssueClosed(DiscussionModel discussion) =>
        WriteObject(new
        {
            number = discussion.Number,
            status = discussion.Status,
        });

    public void WriteIssueList(IReadOnlyList<DiscussionModel> discussions) =>
        WriteObject(new
        {
            issues = discussions.Select(d => new
            {
                number = d.Number,
                title = d.Title,
                status = d.Status,
                updatedAt = d.UpdatedAt,
            }),
        });

    public void WriteIssueView(DiscussionModel discussion, string url) =>
        WriteObject(new
        {
            number = discussion.Number,
            title = discussion.Title,
            status = discussion.Status,
            creator = discussion.CreatorUsername,
            assignee = discussion.AssigneeUsername,
            tags = discussion.Tags.Select(tag => tag.Name),
            url,
            createdAt = discussion.CreatedAt,
            updatedAt = discussion.UpdatedAt,
            comments = discussion.Comments?.Select(comment => new
            {
                id = comment.Id,
                author = comment.AuthorUsername,
                body = comment.BodyMarkdown,
                createdAt = comment.CreatedAt,
            }),
        });

    public void WriteIssueStatus(DiscussionStatus status) => WriteObject(new { status });

    public void WriteError(CliErrorOutput error) => WriteObject(error);

    private void WriteObject(object value)
    {
        _output.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
    }
}
