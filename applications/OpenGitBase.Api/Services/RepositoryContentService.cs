#pragma warning disable SA1204 // Static members should appear before non-static members
#pragma warning disable SA1412 // Store files as UTF-8 with byte order mark
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Models.StorageContent;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class RepositoryContentService : IRepositoryDiskUsageProvider
{
    private const string LfsOidHeader = "X-OpenGitBase-Lfs-Oid";

    /// <summary>
    /// Raw LFS content gets its real type only for plain images the blob view previews. Everything else, SVG and
    /// HTML included, stays a download so user content never runs in the API's origin.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> InlineImageTypes = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
    };

    private readonly RepositoryContentAuthorizationService _authorization;
    private readonly IQueryProcessor _queryProcessor;
    private readonly WebReadReplicaSelector _replicaSelector;
    private readonly IStorageContentClient _storageContentClient;
    private readonly IRepositoryContentCache _cache;
    private readonly ILfsContentReader _lfsContent;

    public RepositoryContentService(
        RepositoryContentAuthorizationService authorization,
        IQueryProcessor queryProcessor,
        WebReadReplicaSelector replicaSelector,
        IStorageContentClient storageContentClient,
        IRepositoryContentCache cache,
        ILfsContentReader lfsContent
    )
    {
        _authorization = authorization;
        _queryProcessor = queryProcessor;
        _replicaSelector = replicaSelector;
        _storageContentClient = storageContentClient;
        _cache = cache;
        _lfsContent = lfsContent;
    }

    public async Task<(RepositoryContentAccessResult Access, RepositoryContentRefsResponse? Data)> GetRefsAsync(
        string owner,
        string slug,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization.AuthorizeReadAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);
        if (access.Kind != RepositoryContentAccessResultKind.Allowed || access.Repository is null)
        {
            return (access, null);
        }

        var cacheKey = RepositoryContentCacheKeys.Build(access.Repository.Id.Value, "refs", "-", "-");
        var cached = await _cache
            .GetAsync<RepositoryContentRefsResponse>(cacheKey, cancellationToken)
            .ConfigureAwait(false);
        if (cached is not null)
        {
            return (access, cached);
        }

        var context = await LoadStorageContextAsync(access.Repository, cancellationToken)
            .ConfigureAwait(false);
        if (context is null)
        {
            return (RepositoryContentAccessResult.Unavailable(), null);
        }

        var payload = await _storageContentClient
            .GetRefsAsync(context.Target, context.ApiToken, context.PhysicalPath, cancellationToken)
            .ConfigureAwait(false);
        if (payload is null)
        {
            return (RepositoryContentAccessResult.Unavailable(), null);
        }

        var branches = payload.Branches
            .Select(item => new RepositoryContentRefDto
            {
                Name = item.Name,
                CommitSha = item.CommitSha,
            })
            .ToList();
        var tags = payload.Tags
            .Select(item => new RepositoryContentRefDto
            {
                Name = item.Name,
                CommitSha = item.CommitSha,
            })
            .ToList();

        var repository = access.Repository;
        if (string.IsNullOrWhiteSpace(repository.DefaultBranchName) && branches.Count > 0)
        {
            var inferred = DefaultRefResolver.Resolve(branches);
            if (!string.IsNullOrWhiteSpace(inferred))
            {
                var updated = await _queryProcessor
                    .RunQueryAsync(
                        new UpdateRepositoryDefaultBranchQuery
                        {
                            RepositoryId = repository.Id,
                            DefaultBranchName = inferred,
                            AllowMissingBranch = true,
                        },
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (updated.IsSome)
                {
                    repository = updated.Get();
                }
            }
        }

        var response = new RepositoryContentRefsResponse
        {
            Branches = branches,
            Tags = tags,
            DefaultRef = DefaultRefResolver.Resolve(branches, repository.DefaultBranchName),
            IsEmpty = payload.IsEmpty,
            ReplicationLag = context.ReplicationLag,
        };

        await _cache
            .SetAsync(cacheKey, response, RepositoryContentCacheTtl.Default, cancellationToken)
            .ConfigureAwait(false);

        return (access, response);
    }

    public async Task<(RepositoryContentAccessResult Access, RepositoryContentTreeResponse? Data)> GetTreeAsync(
        string owner,
        string slug,
        string refName,
        string path,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization.AuthorizeReadAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);
        if (access.Kind != RepositoryContentAccessResultKind.Allowed || access.Repository is null)
        {
            return (access, null);
        }

        var cacheKey = RepositoryContentCacheKeys.Build(
            access.Repository.Id.Value,
            "tree",
            refName,
            path
        );
        var cached = await _cache
            .GetAsync<RepositoryContentTreeResponse>(cacheKey, cancellationToken)
            .ConfigureAwait(false);
        if (cached is not null)
        {
            return (access, cached);
        }

        var context = await LoadStorageContextAsync(access.Repository, cancellationToken)
            .ConfigureAwait(false);
        if (context is null)
        {
            return (RepositoryContentAccessResult.Unavailable(), null);
        }

        var tree = await _storageContentClient
            .GetTreeAsync(
                context.Target,
                context.ApiToken,
                context.PhysicalPath,
                refName,
                path,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (tree.IsMissing)
        {
            return (RepositoryContentAccessResult.NotFound(), null);
        }

        var payload = tree.Payload;
        if (payload is null)
        {
            return (RepositoryContentAccessResult.Unavailable(), null);
        }

        var response = new RepositoryContentTreeResponse
        {
            Ref = payload.Ref,
            Path = payload.Path,
            Entries = DirectoryEntrySorter.Sort(
                payload.Entries.Select(entry => new RepositoryContentEntryDto
                {
                    Name = entry.Name,
                    Path = entry.Path,
                    Type = entry.Type,
                    Size = entry.Size,
                    LastModifiedAt = entry.LastModifiedAt,
                })
            ),
            ReplicationLag = context.ReplicationLag,
        };

        await _cache
            .SetAsync(cacheKey, response, RepositoryContentCacheTtl.Default, cancellationToken)
            .ConfigureAwait(false);

        return (access, response);
    }

    public async Task<(RepositoryContentAccessResult Access, RepositoryContentBlobResponse? Data)> GetBlobAsync(
        string owner,
        string slug,
        string refName,
        string path,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization.AuthorizeReadAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);
        if (access.Kind != RepositoryContentAccessResultKind.Allowed || access.Repository is null)
        {
            return (access, null);
        }

        var cacheKey = RepositoryContentCacheKeys.Build(
            access.Repository.Id.Value,
            "blob",
            refName,
            path
        );
        var cached = await _cache
            .GetAsync<RepositoryContentBlobResponse>(cacheKey, cancellationToken)
            .ConfigureAwait(false);
        if (cached is not null)
        {
            return (access, cached);
        }

        var context = await LoadStorageContextAsync(access.Repository, cancellationToken)
            .ConfigureAwait(false);
        if (context is null)
        {
            return (RepositoryContentAccessResult.Unavailable(), null);
        }

        var blob = await _storageContentClient
            .GetBlobAsync(
                context.Target,
                context.ApiToken,
                context.PhysicalPath,
                refName,
                path,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (blob.IsMissing)
        {
            return (RepositoryContentAccessResult.NotFound(), null);
        }

        var payload = blob.Payload;
        if (payload is null)
        {
            return (RepositoryContentAccessResult.Unavailable(), null);
        }

        RepositoryContentLfsResponse? lfs = null;
        if (payload.Lfs is { } pointer)
        {
            var recorded = await _lfsContent
                .FindRecordedSizeAsync(access.Repository.Id.Value, pointer.Oid, cancellationToken)
                .ConfigureAwait(false);
            lfs = new RepositoryContentLfsResponse { Oid = pointer.Oid, Size = pointer.Size, Available = recorded is not null };
        }

        var response = new RepositoryContentBlobResponse
        {
            Ref = payload.Ref,
            Path = payload.Path,
            Size = payload.Size,
            IsBinary = payload.IsBinary,
            IsTooLarge = payload.IsTooLarge,
            PreviewKind = payload.PreviewKind,
            TextContent = payload.TextContent,
            ContentBase64 = payload.ContentBase64,
            Lfs = lfs,
            ReplicationLag = context.ReplicationLag,
        };

        // An LFS object's availability changes with uploads and repairs; pointer blobs are looked up every time.
        if (lfs is not null)
        {
            return (access, response);
        }

        await _cache
            .SetAsync(cacheKey, response, RepositoryContentCacheTtl.Default, cancellationToken)
            .ConfigureAwait(false);

        return (access, response);
    }

    public async Task<(RepositoryContentAccessResult Access, RepositoryContentReadmeResponse? Data)> GetReadmeAsync(
        string owner,
        string slug,
        string refName,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization.AuthorizeReadAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);
        if (access.Kind != RepositoryContentAccessResultKind.Allowed || access.Repository is null)
        {
            return (access, null);
        }

        var cacheKey = RepositoryContentCacheKeys.Build(
            access.Repository.Id.Value,
            "readme",
            refName,
            "-"
        );
        var cached = await _cache
            .GetAsync<RepositoryContentReadmeResponse>(cacheKey, cancellationToken)
            .ConfigureAwait(false);
        if (cached is not null)
        {
            return (access, cached);
        }

        var context = await LoadStorageContextAsync(access.Repository, cancellationToken)
            .ConfigureAwait(false);
        if (context is null)
        {
            return (RepositoryContentAccessResult.Unavailable(), null);
        }

        var payload = await _storageContentClient
            .GetReadmeAsync(
                context.Target,
                context.ApiToken,
                context.PhysicalPath,
                refName,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (payload is null)
        {
            return (access, null);
        }

        var response = new RepositoryContentReadmeResponse
        {
            Ref = payload.Ref,
            FileName = payload.FileName,
            MarkdownSource = payload.MarkdownSource,
            ReplicationLag = context.ReplicationLag,
        };

        await _cache
            .SetAsync(cacheKey, response, RepositoryContentCacheTtl.Default, cancellationToken)
            .ConfigureAwait(false);

        return (access, response);
    }

    public async Task<long?> GetDiskUsageBytesAsync(
        RepositoryDto repository,
        CancellationToken cancellationToken
    )
    {
        var context = await LoadStorageContextAsync(repository, cancellationToken)
            .ConfigureAwait(false);
        if (context is null)
        {
            return null;
        }

        var payload = await _storageContentClient
            .GetDiskUsageAsync(
                context.Target,
                context.ApiToken,
                context.PhysicalPath,
                cancellationToken
            )
            .ConfigureAwait(false);

        return payload?.BytesUsed;
    }

    public async Task<(RepositoryContentAccessResult Access, StorageRawResult? Data)> GetRawAsync(
        string owner,
        string slug,
        string refName,
        string path,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization.AuthorizeReadAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);
        if (access.Kind != RepositoryContentAccessResultKind.Allowed || access.Repository is null)
        {
            return (access, null);
        }

        var context = await LoadStorageContextAsync(access.Repository, cancellationToken)
            .ConfigureAwait(false);
        if (context is null)
        {
            return (RepositoryContentAccessResult.Unavailable(), null);
        }

        var response = await _storageContentClient
            .GetRawAsync(
                context.Target,
                context.ApiToken,
                context.PhysicalPath,
                refName,
                path,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var isMissing = await StorageContentClient
                .IsMissingPathAsync(response, cancellationToken)
                .ConfigureAwait(false);
            response.Dispose();
            return (
                isMissing
                    ? RepositoryContentAccessResult.NotFound()
                    : RepositoryContentAccessResult.Unavailable(),
                null
            );
        }

        var fileName = Path.GetFileName(path);
        if (response.Headers.TryGetValues(LfsOidHeader, out var oids))
        {
            response.Dispose();
            return (
                access,
                await OpenLfsObjectAsync(access.Repository.Id.Value, oids.First(), fileName, cancellationToken)
                    .ConfigureAwait(false)
            );
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        response.Dispose();

        return (
            access,
            new StorageRawResult
            {
                Bytes = bytes,
                FileName = fileName,
                ReplicationLag = context.ReplicationLag,
            }
        );
    }

    public async Task<(RepositoryContentAccessResult Access, RepositoryCommitResponse? Data)> GetCommitAsync(
        string owner,
        string slug,
        string sha,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(sha))
        {
            return (RepositoryContentAccessResult.NotFound(), null);
        }

        var access = await _authorization.AuthorizeReadAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);
        if (access.Kind != RepositoryContentAccessResultKind.Allowed || access.Repository is null)
        {
            return (access, null);
        }

        var cacheKey = RepositoryContentCacheKeys.Build(
            access.Repository.Id.Value,
            "commit",
            sha.Trim(),
            "-"
        );
        var cached = await _cache
            .GetAsync<RepositoryCommitResponse>(cacheKey, cancellationToken)
            .ConfigureAwait(false);
        if (cached is not null)
        {
            return (access, cached);
        }

        var context = await LoadStorageContextAsync(access.Repository, cancellationToken)
            .ConfigureAwait(false);
        if (context is null)
        {
            return (RepositoryContentAccessResult.Unavailable(), null);
        }

        var payload = await _storageContentClient
            .GetCommitAsync(context.Target, context.ApiToken, context.PhysicalPath, sha, cancellationToken)
            .ConfigureAwait(false);
        if (payload is null)
        {
            return (access, null);
        }

        var response = MapCommitResponse(payload, context.ReplicationLag);
        await _cache
            .SetAsync(cacheKey, response, RepositoryContentCacheTtl.Default, cancellationToken)
            .ConfigureAwait(false);

        return (access, response);
    }

    public async Task<IReadOnlyList<string>> ListBranchNamesAsync(
        RepositoryDto repository,
        CancellationToken cancellationToken
    )
    {
        var context = await LoadStorageContextAsync(repository, cancellationToken)
            .ConfigureAwait(false);
        if (context is null)
        {
            return [];
        }

        var payload = await _storageContentClient
            .GetRefsAsync(context.Target, context.ApiToken, context.PhysicalPath, cancellationToken)
            .ConfigureAwait(false);

        return payload?.Branches.Select(branch => branch.Name).ToList() ?? [];
    }

    /// <summary>The LFS object behind a pointer blob, streamed from storage with its recorded length.</summary>
    private async Task<StorageRawResult> OpenLfsObjectAsync(
        Guid repositoryId,
        string oid,
        string fileName,
        CancellationToken cancellationToken
    )
    {
        var size = await _lfsContent.FindRecordedSizeAsync(repositoryId, oid, cancellationToken).ConfigureAwait(false);
        if (size is null)
        {
            return new StorageRawResult { Status = StorageRawStatus.LfsObjectNotAvailable, FileName = fileName };
        }

        var content = await _lfsContent.OpenAsync(repositoryId, oid, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            return new StorageRawResult { Status = StorageRawStatus.LfsObjectRestoring, FileName = fileName };
        }

        return new StorageRawResult
        {
            Content = content,
            ContentLength = size,
            ContentType = InlineImageTypes.GetValueOrDefault(Path.GetExtension(fileName), "application/octet-stream"),
            FileName = fileName,
        };
    }

    private async Task<StorageContentContext?> LoadStorageContextAsync(
        RepositoryDto repository,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(repository.PhysicalPath))
        {
            return null;
        }

        var routing = await _queryProcessor
            .RunQueryAsync(
                new RepositoryReplicationRoutingQuery { RepositoryId = repository.Id },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (routing.IsNone)
        {
            return null;
        }

        var selection = _replicaSelector.Select(routing.Get());
        if (selection is null)
        {
            return null;
        }

        var tokenResult = await _queryProcessor
            .RunQueryAsync(
                new GetStorageNodeApiTokenQuery
                {
                    StorageNodeId = StorageNodeId.From(selection.Target.StorageNodeId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (tokenResult.IsNone)
        {
            return null;
        }

        return new StorageContentContext
        {
            Target = selection.Target,
            ApiToken = tokenResult.Get(),
            PhysicalPath = repository.PhysicalPath,
            ReplicationLag = selection.ReplicationLag,
        };
    }

    private static RepositoryCommitResponse MapCommitResponse(
        StorageContentCommitDetailPayload payload,
        RepositoryReplicationLagDto? replicationLag
    )
    {
        var isRoot = string.Equals(payload.Kind, "root", StringComparison.OrdinalIgnoreCase);
        return new RepositoryCommitResponse
        {
            Sha = payload.Sha,
            ShortSha = string.IsNullOrWhiteSpace(payload.ShortSha)
                ? payload.Sha[..Math.Min(8, payload.Sha.Length)]
                : payload.ShortSha,
            Message = CommitMessagePrivacy.RedactEmails(payload.Message),
            AuthorName = payload.AuthorName,
            AuthoredAt = payload.AuthoredAt,
            Parents = payload.Parents
                .Select(parent => new RepositoryCommitParentResponse
                {
                    Sha = parent.Sha,
                    ShortSha = string.IsNullOrWhiteSpace(parent.ShortSha)
                        ? parent.Sha[..Math.Min(8, parent.Sha.Length)]
                        : parent.ShortSha,
                })
                .ToList(),
            Stats = payload.Stats is null
                ? null
                : new RepositoryCommitStatsResponse
                {
                    FilesChanged = payload.Stats.FilesChanged,
                    Insertions = payload.Stats.Insertions,
                    Deletions = payload.Stats.Deletions,
                },
            Kind = payload.Kind,
            DiffFiles = isRoot
                ? []
                : payload.Files.Select(RepositoryDiffMapper.MapCommitFile).ToList(),
            RootFiles = isRoot
                ? payload.Files
                    .Select(file => new RepositoryCommitRootFileResponse
                    {
                        Path = file.Path ?? string.Empty,
                        ChangeType = file.ChangeType ?? "added",
                    })
                    .ToList()
                : [],
            ReplicationLag = replicationLag,
        };
    }

    private sealed class StorageContentContext
    {
        public required RepositoryRoutingTargetDto Target { get; init; }

        public required string ApiToken { get; init; }

        public required string PhysicalPath { get; init; }

        public RepositoryReplicationLagDto? ReplicationLag { get; init; }
    }
}
