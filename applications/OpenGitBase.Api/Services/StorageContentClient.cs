using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OpenGitBase.Api.Models.StorageContent;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class StorageContentClient(HttpClient httpClient) : IStorageContentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public Task<StorageContentRefsPayload?> GetRefsAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        CancellationToken cancellationToken
    ) =>
        GetCombinedRefsAsync(target, apiToken, physicalPath, cancellationToken);

    public Task<StorageContentTreePayload?> GetTreeAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        string path,
        CancellationToken cancellationToken
    )
    {
        var uri =
            $"http://{target.InternalHost}:{target.InternalHttpPort}/internal/repos/content/tree?physicalPath={Uri.EscapeDataString(physicalPath)}&ref={Uri.EscapeDataString(refName)}&path={Uri.EscapeDataString(path)}";
        return GetJsonAsync<StorageContentTreePayload>(uri, apiToken, cancellationToken)
;
    }

    public Task<StorageContentBlobPayload?> GetBlobAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        string path,
        CancellationToken cancellationToken
    )
    {
        var uri =
            $"http://{target.InternalHost}:{target.InternalHttpPort}/internal/repos/content/blob?physicalPath={Uri.EscapeDataString(physicalPath)}&ref={Uri.EscapeDataString(refName)}&path={Uri.EscapeDataString(path)}";
        return GetJsonAsync<StorageContentBlobPayload>(uri, apiToken, cancellationToken)
;
    }

    public Task<StorageContentReadmePayload?> GetReadmeAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        CancellationToken cancellationToken
    )
    {
        var uri =
            $"http://{target.InternalHost}:{target.InternalHttpPort}/internal/repos/content/readme?physicalPath={Uri.EscapeDataString(physicalPath)}&ref={Uri.EscapeDataString(refName)}";
        return GetJsonAsync<StorageContentReadmePayload>(uri, apiToken, cancellationToken)
;
    }

    public async Task<HttpResponseMessage> GetRawAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        string path,
        CancellationToken cancellationToken
    )
    {
        var uri =
            $"http://{target.InternalHost}:{target.InternalHttpPort}/internal/repos/content/blob/raw?physicalPath={Uri.EscapeDataString(physicalPath)}&ref={Uri.EscapeDataString(refName)}&path={Uri.EscapeDataString(path)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        return await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public Task<StorageContentUsagePayload?> GetDiskUsageAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        CancellationToken cancellationToken
    )
    {
        var uri =
            $"http://{target.InternalHost}:{target.InternalHttpPort}/internal/repos/usage?physicalPath={Uri.EscapeDataString(physicalPath)}";
        return GetJsonAsync<StorageContentUsagePayload>(uri, apiToken, cancellationToken)
;
    }

    public Task<StorageContentAheadCountPayload?> GetAheadCountAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string baseRef,
        string headRef,
        CancellationToken cancellationToken
    )
    {
        var uri =
            $"http://{target.InternalHost}:{target.InternalHttpPort}/internal/repos/content/ahead-count?physicalPath={Uri.EscapeDataString(physicalPath)}&baseRef={Uri.EscapeDataString(baseRef)}&headRef={Uri.EscapeDataString(headRef)}";
        return GetJsonAsync<StorageContentAheadCountPayload>(uri, apiToken, cancellationToken)
;
    }

    public Task<StorageContentResolveRefPayload?> ResolveRefAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        CancellationToken cancellationToken
    )
    {
        var uri =
            $"http://{target.InternalHost}:{target.InternalHttpPort}/internal/repos/content/resolve-ref?physicalPath={Uri.EscapeDataString(physicalPath)}&ref={Uri.EscapeDataString(refName)}";
        return GetJsonAsync<StorageContentResolveRefPayload>(uri, apiToken, cancellationToken)
;
    }

    public Task<StorageContentDiffPayload?> GetDiffAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string baseSha,
        string headSha,
        CancellationToken cancellationToken
    )
    {
        var uri =
            $"http://{target.InternalHost}:{target.InternalHttpPort}/internal/repos/content/diff?physicalPath={Uri.EscapeDataString(physicalPath)}&baseSha={Uri.EscapeDataString(baseSha)}&headSha={Uri.EscapeDataString(headSha)}";
        return GetJsonAsync<StorageContentDiffPayload>(uri, apiToken, cancellationToken)
;
    }

    public Task<StorageContentMergeabilityPayload?> CheckMergeabilityAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string targetSha,
        string sourceSha,
        CancellationToken cancellationToken
    )
    {
        var uri =
            $"http://{target.InternalHost}:{target.InternalHttpPort}/internal/repos/content/mergeability?physicalPath={Uri.EscapeDataString(physicalPath)}&targetSha={Uri.EscapeDataString(targetSha)}&sourceSha={Uri.EscapeDataString(sourceSha)}";
        return GetJsonAsync<StorageContentMergeabilityPayload>(uri, apiToken, cancellationToken)
;
    }

    public async Task<StorageContentExecuteMergeResult> ExecuteMergeAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        StorageContentExecuteMergeRequest request,
        CancellationToken cancellationToken
    )
    {
        var uri =
            $"http://{target.InternalHost}:{target.InternalHttpPort}/internal/repos/content/merge?physicalPath={Uri.EscapeDataString(physicalPath)}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(request, JsonOptions),
            System.Text.Encoding.UTF8,
            "application/json"
        );
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            var payload = JsonSerializer.Deserialize<StorageContentExecuteMergeSuccessPayload>(
                body,
                JsonOptions
            );
            return new StorageContentExecuteMergeResult
            {
                Success = true,
                StatusCode = (int)response.StatusCode,
                CommitSha = payload?.CommitSha,
                Strategy = payload?.Strategy,
                TargetRef = payload?.TargetRef,
            };
        }

        string? errorCode = null;
        string? errorMessage = null;
        try
        {
            var errorPayload = JsonSerializer.Deserialize<StorageContentErrorPayload>(body, JsonOptions);
            errorCode = errorPayload?.Code;
            errorMessage = errorPayload?.Error;
        }
        catch (JsonException)
        {
            errorMessage = body;
        }

        return new StorageContentExecuteMergeResult
        {
            Success = false,
            StatusCode = (int)response.StatusCode,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage ?? response.ReasonPhrase,
        };
    }

    public async Task<bool> DeleteRefAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        CancellationToken cancellationToken
    )
    {
        var uri =
            $"http://{target.InternalHost}:{target.InternalHttpPort}/internal/repos/content/delete-ref?physicalPath={Uri.EscapeDataString(physicalPath)}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { refName }, JsonOptions),
                Encoding.UTF8,
                "application/json"
            ),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    private async Task<StorageContentRefsPayload?> GetCombinedRefsAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        CancellationToken cancellationToken
    )
    {
        var branchesUri =
            $"http://{target.InternalHost}:{target.InternalHttpPort}/internal/repos/content/branches?physicalPath={Uri.EscapeDataString(physicalPath)}";
        var tagsUri =
            $"http://{target.InternalHost}:{target.InternalHttpPort}/internal/repos/content/tags?physicalPath={Uri.EscapeDataString(physicalPath)}";
        var emptyUri =
            $"http://{target.InternalHost}:{target.InternalHttpPort}/internal/repos/content/empty?physicalPath={Uri.EscapeDataString(physicalPath)}";

        var branchesTask = GetJsonAsync<StorageContentBranchListPayload>(
            branchesUri,
            apiToken,
            cancellationToken
        );
        var tagsTask = GetJsonAsync<StorageContentTagListPayload>(
            tagsUri,
            apiToken,
            cancellationToken
        );
        var emptyTask = GetJsonAsync<StorageContentEmptyPayload>(emptyUri, apiToken, cancellationToken);

        await Task.WhenAll(branchesTask, tagsTask, emptyTask).ConfigureAwait(false);

        if (branchesTask.Result is null || tagsTask.Result is null || emptyTask.Result is null)
        {
            return null;
        }

        return new StorageContentRefsPayload
        {
            Branches = branchesTask.Result.Branches,
            Tags = tagsTask.Result.Tags,
            IsEmpty = emptyTask.Result.IsEmpty,
        };
    }

    private async Task<T?> GetJsonAsync<T>(
        string uri,
        string apiToken,
        CancellationToken cancellationToken
    )
        where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        using var response = await httpClient.SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed class StorageContentExecuteMergeSuccessPayload
    {
        public string? CommitSha { get; init; }

        public string? Strategy { get; init; }

        public string? TargetRef { get; init; }
    }

    private sealed class StorageContentErrorPayload
    {
        public string? Error { get; init; }

        public string? Code { get; init; }
    }
}
