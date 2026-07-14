using OpenGitBase.Api.Models.StorageContent;
using OpenGitBase.Api.Services;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Tests.Services;

internal sealed class FakeStorageContentClient : IStorageContentClient
{
    public Task<StorageContentRefsPayload?> GetRefsAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        CancellationToken cancellationToken) =>
        Task.FromResult<StorageContentRefsPayload?>(null);

    public Task<StorageContentTreePayload?> GetTreeAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        string path,
        CancellationToken cancellationToken) =>
        Task.FromResult<StorageContentTreePayload?>(null);

    public Task<StorageContentBlobPayload?> GetBlobAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        string path,
        CancellationToken cancellationToken) =>
        Task.FromResult<StorageContentBlobPayload?>(null);

    public Task<StorageContentReadmePayload?> GetReadmeAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        CancellationToken cancellationToken) =>
        Task.FromResult<StorageContentReadmePayload?>(null);

    public Task<HttpResponseMessage> GetRawAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        string path,
        CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));

    public Task<StorageContentUsagePayload?> GetDiskUsageAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        CancellationToken cancellationToken) =>
        Task.FromResult<StorageContentUsagePayload?>(null);

    public Task<StorageContentAheadCountPayload?> GetAheadCountAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string baseRef,
        string headRef,
        CancellationToken cancellationToken) =>
        Task.FromResult<StorageContentAheadCountPayload?>(null);

    public Task<StorageContentResolveRefPayload?> ResolveRefAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        CancellationToken cancellationToken) =>
        Task.FromResult<StorageContentResolveRefPayload?>(null);

    public Task<StorageContentDiffPayload?> GetDiffAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string baseSha,
        string headSha,
        CancellationToken cancellationToken) =>
        Task.FromResult<StorageContentDiffPayload?>(null);

    public Task<StorageContentMergeabilityPayload?> CheckMergeabilityAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string targetSha,
        string sourceSha,
        CancellationToken cancellationToken) =>
        Task.FromResult<StorageContentMergeabilityPayload?>(null);

    public Task<StorageContentExecuteMergeResult> ExecuteMergeAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        StorageContentExecuteMergeRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new StorageContentExecuteMergeResult
        {
            Success = false,
            StatusCode = 503,
            ErrorMessage = "Storage unavailable in test.",
        });

    public Task<StorageContentCommitsPayload?> ListCommitsSinceMergeBaseAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string targetSha,
        string sourceSha,
        CancellationToken cancellationToken) =>
        Task.FromResult<StorageContentCommitsPayload?>(null);

    public Task<StorageContentCommitDetailPayload?> GetCommitAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string sha,
        CancellationToken cancellationToken) =>
        Task.FromResult<StorageContentCommitDetailPayload?>(null);

    public Task<bool?> IsAncestorAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string ancestorSha,
        string descendantSha,
        CancellationToken cancellationToken) =>
        Task.FromResult<bool?>(null);

    public Task<bool> DeleteRefAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
