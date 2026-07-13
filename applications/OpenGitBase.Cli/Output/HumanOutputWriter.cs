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

    public void WriteMergeRequestCreated(MergeRequestModel mergeRequest, string url)
    {
        _output.WriteLine($"Created merge request #{mergeRequest.Number}: {mergeRequest.Title}");
        _output.WriteLine($"Status: {FormatMergeRequestStatus(mergeRequest)}");
        _output.WriteLine(url);
    }

    public void WriteMergeRequestList(IReadOnlyList<MergeRequestModel> mergeRequests)
    {
        if (mergeRequests.Count == 0)
        {
            _output.WriteLine("No merge requests found.");
            return;
        }

        _output.WriteLine($"{"#",-6} {"Status",-10} {"Refs",-24} {"Updated",-20} Title");
        foreach (var mergeRequest in mergeRequests)
        {
            var refs = $"{mergeRequest.SourceRef}→{mergeRequest.TargetRef}";
            _output.WriteLine(
                $"{mergeRequest.Number,-6} {FormatMergeRequestStatus(mergeRequest),-10} {refs,-24} {mergeRequest.UpdatedAt:u} {mergeRequest.Title}");
        }
    }

    public void WriteMergeRequestView(
        MergeRequestModel mergeRequest,
        string url,
        IReadOnlyList<MergeRequestCommitModel>? commits)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"#{mergeRequest.Number} {mergeRequest.Title}");
        builder.AppendLine($"Status: {FormatMergeRequestStatus(mergeRequest)}");
        builder.AppendLine($"Creator: {mergeRequest.CreatorUsername ?? "unknown"}");
        builder.AppendLine($"Refs: {mergeRequest.SourceRef} → {mergeRequest.TargetRef}");
        builder.AppendLine($"Approvals: {mergeRequest.ApprovalCountAtHead}/{mergeRequest.RequiredApprovalCount}");
        builder.AppendLine($"Source SHA: {mergeRequest.SourceHeadSha}");
        builder.AppendLine($"Target SHA: {mergeRequest.TargetBaseSha}");
        if (!string.IsNullOrWhiteSpace(mergeRequest.MergeCommitSha))
        {
            builder.AppendLine($"Merge commit: {mergeRequest.MergeCommitSha}");
        }

        builder.AppendLine($"URL: {url}");
        builder.AppendLine($"Created: {mergeRequest.CreatedAt:u}");
        builder.AppendLine($"Updated: {mergeRequest.UpdatedAt:u}");

        if (!string.IsNullOrWhiteSpace(mergeRequest.Body))
        {
            builder.AppendLine();
            builder.AppendLine(mergeRequest.Body);
        }

        if (commits is { Count: > 0 })
        {
            builder.AppendLine();
            builder.AppendLine("Commits:");
            foreach (var commit in commits)
            {
                builder.AppendLine($"{commit.ShortSha} {commit.Message} ({commit.AuthorName})");
            }
        }

        _output.Write(builder.ToString());
    }

    public void WriteMergeRequestStatus(
        MergeRequestModel mergeRequest,
        MergeRequestMergeabilityModel mergeability)
    {
        var status = FormatMergeRequestStatus(mergeRequest);
        var mergeHint = string.IsNullOrWhiteSpace(mergeability.Message)
            ? mergeability.Status
            : $"{mergeability.Status}: {mergeability.Message}";
        _output.WriteLine($"{status} — {mergeHint}");
    }

    public void WriteMergeRequestDiff(MergeRequestChangesModel changes)
    {
        if (changes.Files.Count == 0)
        {
            _output.WriteLine("No changes.");
            return;
        }

        var builder = new StringBuilder();
        foreach (var file in changes.Files)
        {
            builder.AppendLine($"--- {file.OldPath ?? file.FilePath}");
            builder.AppendLine($"+++ {file.FilePath}");
            foreach (var hunk in file.Hunks)
            {
                builder.AppendLine(hunk.Header);
                foreach (var line in hunk.Lines)
                {
                    var prefix = line.Type switch
                    {
                        "add" => "+",
                        "remove" => "-",
                        _ => " ",
                    };
                    builder.AppendLine($"{prefix}{line.Content}");
                }
            }

            builder.AppendLine();
        }

        _output.Write(builder.ToString());
    }

    public void WriteMergeRequestClosed(MergeRequestModel mergeRequest) =>
        _output.WriteLine($"Merge request #{mergeRequest.Number} is now {mergeRequest.Status}.");

    public void WriteMergeRequestReady(MergeRequestModel mergeRequest) =>
        _output.WriteLine($"Merge request #{mergeRequest.Number} is now {FormatMergeRequestStatus(mergeRequest)}.");

    public void WriteMergeRequestApproved(MergeRequestModel mergeRequest) =>
        _output.WriteLine(
            $"Merge request #{mergeRequest.Number} approved ({mergeRequest.ApprovalCountAtHead}/{mergeRequest.RequiredApprovalCount}) — {FormatMergeRequestStatus(mergeRequest)}.");

    public void WriteMergeRequestEdited(MergeRequestModel mergeRequest) =>
        _output.WriteLine($"Merge request #{mergeRequest.Number} updated.");

    public void WriteMergeRequestMerged(MergeRequestModel mergeRequest)
    {
        _output.WriteLine($"Merge request #{mergeRequest.Number} merged.");
        if (!string.IsNullOrWhiteSpace(mergeRequest.MergeCommitSha))
        {
            _output.WriteLine($"Merge commit: {mergeRequest.MergeCommitSha}");
        }
    }

    public void WriteError(CliErrorOutput error) => _error.WriteLine(error.Error);

    private static string FormatMergeRequestStatus(MergeRequestModel mergeRequest) =>
        mergeRequest.IsDraft && mergeRequest.Status == MergeRequestStatus.Open
            ? "Draft"
            : mergeRequest.Status.ToString();
}
