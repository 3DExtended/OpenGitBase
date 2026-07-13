using OpenGitBase.Cli.Api.Models;

namespace OpenGitBase.Cli.Commands;

public static class MergeRequestCommandHandlers
{
    public static async Task<int> ListAsync(CliServices services, string? statusFilter)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            MergeRequestStatus? status = null;
            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                if (!Enum.TryParse<MergeRequestStatus>(statusFilter, ignoreCase: true, out var parsedStatus))
                {
                    throw new InvalidOperationException(
                        "Invalid status filter. Use draft, open, approved, merged, or closed.");
                }

                status = parsedStatus;
            }

            var mergeRequests = await services.ApiClient
                .ListMergeRequestsAsync(repo, status)
                .ConfigureAwait(false);

            services.OutputWriter.WriteMergeRequestList(mergeRequests);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }

    public static async Task<int> ViewAsync(CliServices services, int number, bool includeCommits)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            var mergeRequest = await services.ApiClient
                .GetMergeRequestAsync(repo, number)
                .ConfigureAwait(false);

            IReadOnlyList<MergeRequestCommitModel>? commits = null;
            if (includeCommits)
            {
                commits = await services.ApiClient
                    .ListMergeRequestCommitsAsync(repo, number)
                    .ConfigureAwait(false);
            }

            var url = RepoContextResolver.BuildMergeRequestUrl(services.Host, repo, mergeRequest.Number);
            services.OutputWriter.WriteMergeRequestView(mergeRequest, url, commits);
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
            var mergeRequest = await services.ApiClient
                .GetMergeRequestAsync(repo, number)
                .ConfigureAwait(false);
            var mergeability = await services.ApiClient
                .GetMergeRequestMergeabilityAsync(repo, number)
                .ConfigureAwait(false);

            services.OutputWriter.WriteMergeRequestStatus(mergeRequest, mergeability);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }

    public static async Task<int> DiffAsync(CliServices services, int number)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            var changes = await services.ApiClient
                .GetMergeRequestChangesAsync(repo, number)
                .ConfigureAwait(false);

            services.OutputWriter.WriteMergeRequestDiff(changes);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }

    public static async Task<int> CreateAsync(
        CliServices services,
        string title,
        string? body,
        FileInfo? bodyFile,
        string? head,
        string? targetBase,
        bool draft)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            var resolvedBody = BodyContentResolver.Resolve(body, bodyFile);

            var sourceRef = head;
            if (string.IsNullOrWhiteSpace(sourceRef))
            {
                if (!services.GitBranchResolver.TryGetCurrentBranch(services.WorkingDirectory, out var currentBranch))
                {
                    throw new InvalidOperationException(
                        "Could not determine source branch. Pass --head or run inside a git repository.");
                }

                sourceRef = currentBranch;
            }

            var targetRef = targetBase;
            if (string.IsNullOrWhiteSpace(targetRef))
            {
                var summary = await services.ApiClient
                    .GetBranchAheadSummaryAsync(repo, sourceRef)
                    .ConfigureAwait(false);
                targetRef = summary.DefaultRef;
                if (string.IsNullOrWhiteSpace(targetRef))
                {
                    throw new InvalidOperationException(
                        "Could not determine target branch. Pass --base explicitly.");
                }
            }

            var mergeRequest = await services.ApiClient
                .CreateMergeRequestAsync(
                    repo,
                    new CreateMergeRequestRequest
                    {
                        Title = title,
                        Body = resolvedBody,
                        SourceRef = sourceRef,
                        TargetRef = targetRef,
                        IsDraft = draft,
                    })
                .ConfigureAwait(false);

            var url = RepoContextResolver.BuildMergeRequestUrl(services.Host, repo, mergeRequest.Number);
            services.OutputWriter.WriteMergeRequestCreated(mergeRequest, url);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }

    public static async Task<int> CloseAsync(CliServices services, int number)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            var mergeRequest = await services.ApiClient
                .CloseMergeRequestAsync(repo, number)
                .ConfigureAwait(false);

            services.OutputWriter.WriteMergeRequestClosed(mergeRequest);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }

    public static async Task<int> ReadyAsync(CliServices services, int number)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            var mergeRequest = await services.ApiClient
                .PublishMergeRequestAsync(repo, number)
                .ConfigureAwait(false);

            services.OutputWriter.WriteMergeRequestReady(mergeRequest);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }

    public static async Task<int> EditAsync(
        CliServices services,
        int number,
        string? title,
        string? body,
        FileInfo? bodyFile)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(title) && body is null && bodyFile is null)
            {
                throw new InvalidOperationException("At least one of --title, --body, or --body-file is required.");
            }

            var repo = RepoContextResolver.ResolveRepo(services);
            var resolvedBody = BodyContentResolver.Resolve(body, bodyFile);
            var mergeRequest = await services.ApiClient
                .UpdateMergeRequestAsync(
                    repo,
                    number,
                    new UpdateMergeRequestRequest
                    {
                        Title = title,
                        Body = resolvedBody,
                    })
                .ConfigureAwait(false);

            services.OutputWriter.WriteMergeRequestEdited(mergeRequest);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }

    public static async Task<int> ApproveAsync(CliServices services, int number)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            var mergeRequest = await services.ApiClient
                .ApproveMergeRequestAsync(repo, number)
                .ConfigureAwait(false);

            services.OutputWriter.WriteMergeRequestApproved(mergeRequest);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }

    public static async Task<int> MergeAsync(
        CliServices services,
        int number,
        string? strategy,
        bool deleteBranch)
    {
        try
        {
            var mergeStrategy = MergeRequestMergeStrategy.MergeCommit;
            if (!string.IsNullOrWhiteSpace(strategy))
            {
                mergeStrategy = strategy.Trim().ToLowerInvariant() switch
                {
                    "merge-commit" or "merge" => MergeRequestMergeStrategy.MergeCommit,
                    "squash" => MergeRequestMergeStrategy.Squash,
                    "fast-forward" or "ff" => MergeRequestMergeStrategy.FastForward,
                    _ => throw new InvalidOperationException(
                        "Invalid merge strategy. Use merge-commit, squash, or fast-forward."),
                };
            }

            var repo = RepoContextResolver.ResolveRepo(services);
            var mergeRequest = await services.ApiClient
                .MergeMergeRequestAsync(
                    repo,
                    number,
                    new MergeMergeRequestRequest
                    {
                        Strategy = mergeStrategy,
                        DeleteSourceBranch = deleteBranch,
                    })
                .ConfigureAwait(false);

            services.OutputWriter.WriteMergeRequestMerged(mergeRequest);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }
}
