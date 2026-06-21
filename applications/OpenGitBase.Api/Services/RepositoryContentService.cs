﻿using OpenGitBase.Api.Models;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class RepositoryContentService
{
    private readonly RepositoryContentAuthorizationService _authorization;
    private readonly IQueryProcessor _queryProcessor;
    private readonly WebReadReplicaSelector _replicaSelector;
    private readonly IStorageContentClient _storageContentClient;
    private readonly IRepositoryContentCache _cache;

    public RepositoryContentService(
        RepositoryContentAuthorizationService authorization,
        IQueryProcessor queryProcessor,
        WebReadReplicaSelector replicaSelector,
        IStorageContentClient storageContentClient,
        IRepositoryContentCache cache
    )
    {
        _authorization = authorization;
        _queryProcessor = queryProcessor;
        _replicaSelector = replicaSelector;
        _storageContentClient = storageContentClient;
        _cache = cache;
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

        var response = new RepositoryContentRefsResponse
        {
            Branches = branches,
            Tags = tags,
            DefaultRef = DefaultRefResolver.Resolve(branches),
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

        var payload = await _storageContentClient
            .GetTreeAsync(
                context.Target,
                context.ApiToken,
                context.PhysicalPath,
                refName,
                path,
                cancellationToken
            )
            .ConfigureAwait(false);
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

        var payload = await _storageContentClient
            .GetBlobAsync(
                context.Target,
                context.ApiToken,
                context.PhysicalPath,
                refName,
                path,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (payload is null)
        {
            return (RepositoryContentAccessResult.Unavailable(), null);
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
            ReplicationLag = context.ReplicationLag,
        };

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
            response.Dispose();
            return (RepositoryContentAccessResult.Unavailable(), null);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var fileName = Path.GetFileName(path);
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

    private sealed class StorageContentContext
    {
        public required RepositoryRoutingTargetDto Target { get; init; }

        public required string ApiToken { get; init; }

        public required string PhysicalPath { get; init; }

        public RepositoryReplicationLagDto? ReplicationLag { get; init; }
    }
}
