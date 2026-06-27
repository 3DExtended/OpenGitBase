using OpenGitBase.Api.Models.StorageContent;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

public interface IStorageContentClient
{
    Task<StorageContentRefsPayload?> GetRefsAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        CancellationToken cancellationToken
    );

    Task<StorageContentTreePayload?> GetTreeAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        string path,
        CancellationToken cancellationToken
    );

    Task<StorageContentBlobPayload?> GetBlobAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        string path,
        CancellationToken cancellationToken
    );

    Task<StorageContentReadmePayload?> GetReadmeAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        CancellationToken cancellationToken
    );

    Task<HttpResponseMessage> GetRawAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        string path,
        CancellationToken cancellationToken
    );

    Task<StorageContentUsagePayload?> GetDiskUsageAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        CancellationToken cancellationToken
    );

    Task<StorageContentAheadCountPayload?> GetAheadCountAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string baseRef,
        string headRef,
        CancellationToken cancellationToken
    );

    Task<StorageContentResolveRefPayload?> ResolveRefAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        CancellationToken cancellationToken
    );

    Task<StorageContentDiffPayload?> GetDiffAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string baseSha,
        string headSha,
        CancellationToken cancellationToken
    );

    Task<StorageContentMergeabilityPayload?> CheckMergeabilityAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string targetSha,
        string sourceSha,
        CancellationToken cancellationToken
    );

    Task<StorageContentExecuteMergeResult> ExecuteMergeAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        StorageContentExecuteMergeRequest request,
        CancellationToken cancellationToken
    );

    Task<bool> DeleteRefAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        CancellationToken cancellationToken
    );
}
