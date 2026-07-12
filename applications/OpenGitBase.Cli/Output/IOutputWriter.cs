using OpenGitBase.Cli.Api.Models;

namespace OpenGitBase.Cli.Output;

public interface IOutputWriter
{
    void WriteAuthStatus(AuthStatusOutput status);

    void WriteIssueCreated(DiscussionModel discussion, string url);

    void WriteIssueComment(DiscussionCommentModel comment, int issueNumber);

    void WriteIssueClosed(DiscussionModel discussion);

    void WriteIssueList(IReadOnlyList<DiscussionModel> discussions);

    void WriteIssueView(DiscussionModel discussion, string url);

    void WriteIssueStatus(DiscussionStatus status);

    void WriteError(CliErrorOutput error);
}
