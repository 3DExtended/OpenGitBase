#pragma warning disable SA1204 // Static members should appear before non-static members
#pragma warning disable SA1412 // Store files as UTF-8 with byte order mark
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Models.StorageContent;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class MergeRequestCompareService
{
    private readonly MergeRequestRefService _refService;
    private readonly IStorageContentClient _storageContentClient;

    public MergeRequestCompareService(
        MergeRequestRefService refService,
        IStorageContentClient storageContentClient
    )
    {
        _refService = refService;
        _storageContentClient = storageContentClient;
    }

    public async Task<MergeRequestChangesResponse?> GetChangesAsync(
        RepositoryDto repository,
        MergeRequestDto mergeRequest,
        CancellationToken cancellationToken
    )
    {
        var shas = await ResolveCompareShasAsync(repository, mergeRequest, cancellationToken)
            .ConfigureAwait(false);
        if (shas is null)
        {
            return null;
        }

        var context = await _refService
            .LoadStorageContextAsync(repository, cancellationToken)
            .ConfigureAwait(false);
        if (context is null)
        {
            return null;
        }

        var payload = await _storageContentClient
            .GetDiffAsync(
                context.Target,
                context.ApiToken,
                context.PhysicalPath,
                shas.TargetBaseSha,
                shas.SourceHeadSha,
                cancellationToken
            )
            .ConfigureAwait(false);

        return payload is null ? null : MapChanges(payload);
    }

    public async Task<IReadOnlyList<MergeRequestCommitResponse>?> ListCommitsAsync(
        RepositoryDto repository,
        MergeRequestDto mergeRequest,
        CancellationToken cancellationToken
    )
    {
        var shas = await ResolveCompareShasAsync(repository, mergeRequest, cancellationToken)
            .ConfigureAwait(false);
        if (shas is null)
        {
            return null;
        }

        var context = await _refService
            .LoadStorageContextAsync(repository, cancellationToken)
            .ConfigureAwait(false);
        if (context is null)
        {
            return null;
        }

        var payload = await _storageContentClient
            .ListCommitsSinceMergeBaseAsync(
                context.Target,
                context.ApiToken,
                context.PhysicalPath,
                shas.TargetBaseSha,
                shas.SourceHeadSha,
                cancellationToken
            )
            .ConfigureAwait(false);

        return payload?.Commits.Select(MapCommit).ToList();
    }

    private async Task<MergeRequestRefResolution?> ResolveCompareShasAsync(
        RepositoryDto repository,
        MergeRequestDto mergeRequest,
        CancellationToken cancellationToken
    )
    {
        var resolved = await _refService
            .ResolveRefShasAsync(
                repository,
                mergeRequest.SourceRef,
                mergeRequest.TargetRef,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (resolved is null)
        {
            if (
                string.IsNullOrWhiteSpace(mergeRequest.SourceHeadSha)
                || string.IsNullOrWhiteSpace(mergeRequest.TargetBaseSha)
            )
            {
                return null;
            }

            return new MergeRequestRefResolution
            {
                SourceHeadSha = mergeRequest.SourceHeadSha,
                TargetBaseSha = mergeRequest.TargetBaseSha,
            };
        }

        return resolved;
    }

    private static MergeRequestChangesResponse MapChanges(StorageContentDiffPayload payload) =>
        new()
        {
            Files = payload.Files.Select(MapFile).ToList(),
        };

    private static MergeRequestDiffFileResponse MapFile(StorageContentDiffFilePayload file)
    {
        var filePath = file.NewPath ?? file.OldPath ?? string.Empty;
        return new MergeRequestDiffFileResponse
        {
            FilePath = filePath,
            OldPath = file.OldPath,
            ChangeType = file.Status,
            Hunks = file.Hunks.Select(MapHunk).ToList(),
        };
    }

    private static MergeRequestDiffHunkResponse MapHunk(StorageContentDiffHunkPayload hunk) =>
        new()
        {
            Header =
                $"@@ -{hunk.OldStart},{hunk.OldLines} +{hunk.NewStart},{hunk.NewLines} @@",
            Lines = hunk.Lines.Select(MapLine).ToList(),
        };

    private static MergeRequestDiffLineResponse MapLine(StorageContentDiffLinePayload line) =>
        new()
        {
            OldLineNumber = line.OldLineNumber,
            NewLineNumber = line.NewLineNumber,
            Type = string.Equals(line.Type, "delete", StringComparison.OrdinalIgnoreCase)
                ? "remove"
                : line.Type,
            Content = line.Content,
        };

    private static MergeRequestCommitResponse MapCommit(StorageContentCommitPayload commit) =>
        new()
        {
            Sha = commit.Sha,
            ShortSha = string.IsNullOrWhiteSpace(commit.ShortSha)
                ? commit.Sha[..Math.Min(8, commit.Sha.Length)]
                : commit.ShortSha,
            Message = commit.Message,
            AuthorName = commit.AuthorName,
            AuthoredAt = commit.AuthoredAt,
        };
}
