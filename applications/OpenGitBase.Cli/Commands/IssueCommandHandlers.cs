using OpenGitBase.Cli.Api.Models;

namespace OpenGitBase.Cli.Commands;

public static class IssueCommandHandlers
{
    public static async Task<int> CreateAsync(CliServices services, string title, string? body, FileInfo? bodyFile)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            var bodyParts = DiscussionBodySplitter.Split(BodyContentResolver.Resolve(body, bodyFile));
            var discussion = await services.ApiClient
                .CreateDiscussionAsync(
                    repo,
                    new CreateDiscussionRequest { Title = title, Body = bodyParts.FirstOrDefault() })
                .ConfigureAwait(false);

            for (var index = 1; index < bodyParts.Count; index++)
            {
                try
                {
                    await services.ApiClient
                        .CreateCommentAsync(
                            repo,
                            discussion.Number,
                            new CreateDiscussionCommentRequest { BodyMarkdown = bodyParts[index] })
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Created issue #{discussion.Number}, but posting body part {index + 1} of {bodyParts.Count} "
                        + $"as a comment failed: {ex.Message}",
                        ex);
                }
            }

            var url = RepoContextResolver.BuildDiscussionUrl(services.Host, repo, discussion.Number);
            services.OutputWriter.WriteIssueCreated(discussion, url, continuationComments: Math.Max(0, bodyParts.Count - 1));
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }

    public static async Task<int> CommentAsync(
        CliServices services,
        int number,
        string? body,
        FileInfo? bodyFile)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            var resolvedBody = BodyContentResolver.Resolve(body, bodyFile)
                ?? throw new InvalidOperationException("Comment body is required. Pass --body or --body-file.");

            var comment = await services.ApiClient
                .CreateCommentAsync(
                    repo,
                    number,
                    new CreateDiscussionCommentRequest { BodyMarkdown = resolvedBody })
                .ConfigureAwait(false);

            services.OutputWriter.WriteIssueComment(comment, number);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }

    public static async Task<int> CloseAsync(CliServices services, int number, string? reason)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            var discussion = string.Equals(reason, "dismissed", StringComparison.OrdinalIgnoreCase)
                ? await services.ApiClient.DismissDiscussionAsync(repo, number).ConfigureAwait(false)
                : await services.ApiClient.ResolveDiscussionAsync(repo, number).ConfigureAwait(false);

            services.OutputWriter.WriteIssueClosed(discussion);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }

    public static async Task<int> ListAsync(CliServices services, string? statusFilter)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            DiscussionStatus? status = null;
            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                if (!Enum.TryParse<DiscussionStatus>(statusFilter, ignoreCase: true, out var parsedStatus))
                {
                    throw new InvalidOperationException(
                        "Invalid status filter. Use open, engaged, resolved, or dismissed.");
                }

                status = parsedStatus;
            }

            var discussions = await services.ApiClient
                .ListDiscussionsAsync(repo, status)
                .ConfigureAwait(false);

            services.OutputWriter.WriteIssueList(discussions);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }

    public static async Task<int> ViewAsync(CliServices services, int number)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            var discussion = await services.ApiClient
                .GetDiscussionAsync(repo, number, includeComments: true)
                .ConfigureAwait(false);

            var url = RepoContextResolver.BuildDiscussionUrl(services.Host, repo, discussion.Number);
            services.OutputWriter.WriteIssueView(discussion, url);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }

    public static async Task<int> StatusAsync(CliServices services, int number)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            var discussion = await services.ApiClient
                .GetDiscussionAsync(repo, number, includeComments: false)
                .ConfigureAwait(false);

            services.OutputWriter.WriteIssueStatus(discussion.Status);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }
}
