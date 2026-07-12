using System.Text;
using OpenGitBase.Cli.Api.Models;

namespace OpenGitBase.Cli.Output;

public sealed class HumanOutputWriter : IOutputWriter
{
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public HumanOutputWriter(TextWriter output, TextWriter error)
    {
        _output = output;
        _error = error;
    }

    public void WriteAuthStatus(AuthStatusOutput status)
    {
        if (!status.LoggedIn)
        {
            _output.WriteLine("Not logged in.");
            return;
        }

        _output.WriteLine($"Logged in to {status.Hostname} as {status.Username}.");
    }

    public void WriteIssueCreated(DiscussionModel discussion, string url)
    {
        _output.WriteLine($"Created issue #{discussion.Number}: {discussion.Title}");
        _output.WriteLine(url);
    }

    public void WriteIssueComment(DiscussionCommentModel comment, int issueNumber)
    {
        _output.WriteLine($"Comment added to issue #{issueNumber}.");
        _output.WriteLine($"Author: {comment.AuthorUsername ?? "unknown"}");
    }

    public void WriteIssueClosed(DiscussionModel discussion)
    {
        _output.WriteLine($"Issue #{discussion.Number} is now {discussion.Status}.");
    }

    public void WriteIssueList(IReadOnlyList<DiscussionModel> discussions)
    {
        if (discussions.Count == 0)
        {
            _output.WriteLine("No issues found.");
            return;
        }

        _output.WriteLine($"{"#",-6} {"Status",-10} {"Updated",-20} Title");
        foreach (var discussion in discussions)
        {
            _output.WriteLine(
                $"{discussion.Number,-6} {discussion.Status,-10} {discussion.UpdatedAt:u} {discussion.Title}");
        }
    }

    public void WriteIssueView(DiscussionModel discussion, string url)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"#{discussion.Number} {discussion.Title}");
        builder.AppendLine($"Status: {discussion.Status}");
        builder.AppendLine($"Creator: {discussion.CreatorUsername ?? "unknown"}");
        if (!string.IsNullOrWhiteSpace(discussion.AssigneeUsername))
        {
            builder.AppendLine($"Assignee: {discussion.AssigneeUsername}");
        }

        if (discussion.Tags.Count > 0)
        {
            builder.AppendLine($"Tags: {string.Join(", ", discussion.Tags.Select(tag => tag.Name))}");
        }

        builder.AppendLine($"URL: {url}");
        builder.AppendLine($"Created: {discussion.CreatedAt:u}");
        builder.AppendLine($"Updated: {discussion.UpdatedAt:u}");

        if (discussion.Comments is { Count: > 0 })
        {
            builder.AppendLine();
            builder.AppendLine("Comments:");
            foreach (var comment in discussion.Comments)
            {
                builder.AppendLine($"--- {comment.AuthorUsername ?? "unknown"} @ {comment.CreatedAt:u} ---");
                builder.AppendLine(comment.BodyMarkdown);
                builder.AppendLine();
            }
        }

        _output.Write(builder.ToString());
    }

    public void WriteIssueStatus(DiscussionStatus status) => _output.WriteLine(status);

    public void WriteError(CliErrorOutput error) => _error.WriteLine(error.Error);
}
