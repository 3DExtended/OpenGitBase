using System.Net.Http.Headers;
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

    public async Task<StorageContentTreePayload?> GetTreeAsync(
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
        return await GetJsonAsync<StorageContentTreePayload>(uri, apiToken, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<StorageContentBlobPayload?> GetBlobAsync(
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
        return await GetJsonAsync<StorageContentBlobPayload>(uri, apiToken, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<StorageContentReadmePayload?> GetReadmeAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string refName,
        CancellationToken cancellationToken
    )
    {
        var uri =
            $"http://{target.InternalHost}:{target.InternalHttpPort}/internal/repos/content/readme?physicalPath={Uri.EscapeDataString(physicalPath)}&ref={Uri.EscapeDataString(refName)}";
        return await GetJsonAsync<StorageContentReadmePayload>(uri, apiToken, cancellationToken)
            .ConfigureAwait(false);
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

    public async Task<StorageContentUsagePayload?> GetDiskUsageAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        CancellationToken cancellationToken
    )
    {
        var uri =
            $"http://{target.InternalHost}:{target.InternalHttpPort}/internal/repos/usage?physicalPath={Uri.EscapeDataString(physicalPath)}";
        return await GetJsonAsync<StorageContentUsagePayload>(uri, apiToken, cancellationToken)
            .ConfigureAwait(false);
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
}
