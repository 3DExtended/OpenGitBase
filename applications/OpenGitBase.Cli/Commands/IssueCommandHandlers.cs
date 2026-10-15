using OpenGitBase.Cli.Api;
using OpenGitBase.Cli.Api.Models;
using OpenGitBase.Cli.Auth;

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

    public static async Task<int> CommentEditAsync(
        CliServices services,
        Guid commentId,
        string? body,
        FileInfo? bodyFile)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            var resolvedBody = BodyContentResolver.Resolve(body, bodyFile)
                ?? throw new InvalidOperationException("Comment body is required. Pass --body or --body-file.");
            var length = resolvedBody.Trim().Length;
            if (length > DiscussionBodySplitter.MaxCommentLength)
            {
                throw new InvalidOperationException(
                    $"Comment text is {length} characters; a comment holds at most "
                    + $"{DiscussionBodySplitter.MaxCommentLength}.");
            }

            DiscussionCommentModel comment;
            try
            {
                comment = await services.ApiClient
                    .UpdateCommentAsync(repo, commentId, new UpdateDiscussionCommentRequest { BodyMarkdown = resolvedBody })
                    .ConfigureAwait(false);
            }
            catch (OgbApiException ex) when (ex.HttpStatus == 404)
            {
                throw new OgbApiException(
                    $"Comment {commentId} was not found, is deleted, or belongs to someone else; only its author can edit it.",
                    ex.HttpStatus,
                    ex.Detail);
            }

            services.OutputWriter.WriteIssueCommentEdited(comment);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }

    public static async Task<int> EditAsync(CliServices services, int number, string? body, FileInfo? bodyFile)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            var text = BodyContentResolver.Resolve(body, bodyFile);
            var discussion = await services.ApiClient
                .GetDiscussionAsync(repo, number, includeComments: true)
                .ConfigureAwait(false);
            EnsureSignedInAsCreator(services, discussion);
            var rewrite = DiscussionSpec.PlanRewrite(discussion, text);

            var updatedParts = 0;
            foreach (var edit in rewrite.Edits)
            {
                try
                {
                    await services.ApiClient
                        .UpdateCommentAsync(repo, edit.CommentId, new UpdateDiscussionCommentRequest { BodyMarkdown = edit.Text })
                        .ConfigureAwait(false);
                    updatedParts++;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Updating spec part {edit.Index + 1} of {rewrite.PartCount} of #{number} failed: {ex.Message} "
                        + $"({updatedParts} part(s) already updated). To finish, run the same command again; "
                        + "it skips parts that already match.",
                        ex);
                }
            }

            foreach (var commentId in rewrite.SurplusCommentIds)
            {
                try
                {
                    await services.ApiClient.DeleteCommentAsync(repo, commentId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"The spec of #{number} is updated, but removing its surplus comment {commentId} failed: "
                        + $"{ex.Message} To finish, run the same command again.",
                        ex);
                }
            }

            var url = RepoContextResolver.BuildDiscussionUrl(services.Host, repo, number);
            services.OutputWriter.WriteIssueEdited(number, url, rewrite);
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

    /// <summary>
    /// The API lets only a comment's author edit it, and the spec comments all belong to the creator. Checking
    /// first turns a mid-rewrite 404 into a clear refusal before anything changes.
    /// </summary>
    private static void EnsureSignedInAsCreator(CliServices services, DiscussionModel discussion)
    {
        var token = services.CredentialStore.GetToken(services.Host);
        var username = token is null ? null : JwtTokenReader.TryGetUsername(token);
        if (username is not null
            && !string.Equals(username, discussion.CreatorUsername, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Only {discussion.CreatorUsername ?? "its creator"}, who published #{discussion.Number}, can edit its spec; "
                + $"you are signed in as {username}.");
        }
    }
}
