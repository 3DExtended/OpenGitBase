using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGitBase.Cli.Api.Models;
using OpenGitBase.Cli.Auth;
using OpenGitBase.Cli.Git;

namespace OpenGitBase.Cli.Api;

public sealed class OgbApiClient : IOgbApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(), new FlexibleGuidJsonConverter() },
    };

    private readonly HttpClient _httpClient;
    private readonly ICredentialStore _credentialStore;
    private readonly string _host;

    public OgbApiClient(HttpClient httpClient, ICredentialStore credentialStore, string host, string apiBaseUrl)
    {
        _httpClient = httpClient;
        _credentialStore = credentialStore;
        _host = host;
        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
        }
    }

    public Task<DiscussionModel> CreateDiscussionAsync(
        RepoSlug repo,
        CreateDiscussionRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<DiscussionModel>(
            HttpMethod.Post,
            OgbApiClientHelpers.BuildDiscussionsPath(repo),
            request,
            cancellationToken);

    public Task<DiscussionCommentModel> CreateCommentAsync(
        RepoSlug repo,
        int number,
        CreateDiscussionCommentRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<DiscussionCommentModel>(
            HttpMethod.Post,
            $"{OgbApiClientHelpers.BuildDiscussionsPath(repo)}/{number}/comments",
            request,
            cancellationToken);

    public Task<DiscussionModel> ResolveDiscussionAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default) =>
        SendAsync<DiscussionModel>(
            HttpMethod.Post,
            $"{OgbApiClientHelpers.BuildDiscussionsPath(repo)}/{number}/resolve",
            body: null,
            cancellationToken);

    public Task<DiscussionModel> DismissDiscussionAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default) =>
        SendAsync<DiscussionModel>(
            HttpMethod.Post,
            $"{OgbApiClientHelpers.BuildDiscussionsPath(repo)}/{number}/dismiss",
            body: null,
            cancellationToken);

    public Task<IReadOnlyList<DiscussionModel>> ListDiscussionsAsync(
        RepoSlug repo,
        DiscussionStatus? status,
        CancellationToken cancellationToken = default)
    {
        var path = OgbApiClientHelpers.BuildDiscussionsPath(repo);
        if (status is not null)
        {
            path += $"?status={status.Value.ToString().ToLowerInvariant()}";
        }

        return SendAsync<IReadOnlyList<DiscussionModel>>(
            HttpMethod.Get,
            path,
            body: null,
            cancellationToken);
    }

    public Task<DiscussionModel> GetDiscussionAsync(
        RepoSlug repo,
        int number,
        bool includeComments,
        CancellationToken cancellationToken = default)
    {
        var path = $"{OgbApiClientHelpers.BuildDiscussionsPath(repo)}/{number}";
        if (includeComments)
        {
            path += "?include=comments";
        }

        return SendAsync<DiscussionModel>(HttpMethod.Get, path, body: null, cancellationToken);
    }

    public Task<IReadOnlyList<MergeRequestModel>> ListMergeRequestsAsync(
        RepoSlug repo,
        MergeRequestStatus? status,
        CancellationToken cancellationToken = default)
    {
        var path = OgbApiClientHelpers.BuildMergeRequestsPath(repo);
        if (status is not null)
        {
            path += $"?status={status.Value.ToString().ToLowerInvariant()}";
        }

        return SendAsync<IReadOnlyList<MergeRequestModel>>(
            HttpMethod.Get,
            path,
            body: null,
            cancellationToken);
    }

    public Task<MergeRequestModel> GetMergeRequestAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default) =>
        SendAsync<MergeRequestModel>(
            HttpMethod.Get,
            $"{OgbApiClientHelpers.BuildMergeRequestsPath(repo)}/{number}",
            body: null,
            cancellationToken);

    public Task<MergeRequestModel> CreateMergeRequestAsync(
        RepoSlug repo,
        CreateMergeRequestRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<MergeRequestModel>(
            HttpMethod.Post,
            OgbApiClientHelpers.BuildMergeRequestsPath(repo),
            request,
            cancellationToken);

    public Task<MergeRequestModel> UpdateMergeRequestAsync(
        RepoSlug repo,
        int number,
        UpdateMergeRequestRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<MergeRequestModel>(
            HttpMethod.Patch,
            $"{OgbApiClientHelpers.BuildMergeRequestsPath(repo)}/{number}",
            request,
            cancellationToken);

    public Task<MergeRequestModel> PublishMergeRequestAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default) =>
        SendAsync<MergeRequestModel>(
            HttpMethod.Post,
            $"{OgbApiClientHelpers.BuildMergeRequestsPath(repo)}/{number}/publish",
            body: null,
            cancellationToken);

    public Task<MergeRequestModel> CloseMergeRequestAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default) =>
        SendAsync<MergeRequestModel>(
            HttpMethod.Post,
            $"{OgbApiClientHelpers.BuildMergeRequestsPath(repo)}/{number}/close",
            body: null,
            cancellationToken);

    public Task<MergeRequestModel> ApproveMergeRequestAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default) =>
        SendAsync<MergeRequestModel>(
            HttpMethod.Post,
            $"{OgbApiClientHelpers.BuildMergeRequestsPath(repo)}/{number}/approve",
            body: null,
            cancellationToken);

    public Task<MergeRequestModel> MergeMergeRequestAsync(
        RepoSlug repo,
        int number,
        MergeMergeRequestRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<MergeRequestModel>(
            HttpMethod.Post,
            $"{OgbApiClientHelpers.BuildMergeRequestsPath(repo)}/{number}/merge",
            request,
            cancellationToken);

    public Task<MergeRequestChangesModel> GetMergeRequestChangesAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default) =>
        SendAsync<MergeRequestChangesModel>(
            HttpMethod.Get,
            $"{OgbApiClientHelpers.BuildMergeRequestsPath(repo)}/{number}/changes",
            body: null,
            cancellationToken);

    public Task<IReadOnlyList<MergeRequestCommitModel>> ListMergeRequestCommitsAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<MergeRequestCommitModel>>(
            HttpMethod.Get,
            $"{OgbApiClientHelpers.BuildMergeRequestsPath(repo)}/{number}/commits",
            body: null,
            cancellationToken);

    public Task<MergeRequestMergeabilityModel> GetMergeRequestMergeabilityAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default) =>
        SendAsync<MergeRequestMergeabilityModel>(
            HttpMethod.Get,
            $"{OgbApiClientHelpers.BuildMergeRequestsPath(repo)}/{number}/mergeability",
            body: null,
            cancellationToken);

    public Task<MergeRequestBranchAheadSummaryModel> GetBranchAheadSummaryAsync(
        RepoSlug repo,
        string refName,
        CancellationToken cancellationToken = default) =>
        SendAsync<MergeRequestBranchAheadSummaryModel>(
            HttpMethod.Get,
            $"{OgbApiClientHelpers.BuildMergeRequestsPath(repo)}/branch-ahead-summary?refName={Uri.EscapeDataString(refName)}",
            body: null,
            cancellationToken);

    public Task<IReadOnlyList<DiscussionLinkModel>> ListDiscussionLinksAsync(
        RepoSlug repo,
        int number,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<DiscussionLinkModel>>(
            HttpMethod.Get,
            $"{OgbApiClientHelpers.BuildDiscussionsPath(repo)}/{number}/links",
            body: null,
            cancellationToken);

    public Task<DiscussionLinkModel> CreateDiscussionLinkAsync(
        RepoSlug repo,
        int number,
        CreateDiscussionLinkRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<DiscussionLinkModel>(
            HttpMethod.Post,
            $"{OgbApiClientHelpers.BuildDiscussionsPath(repo)}/{number}/links",
            request,
            cancellationToken);

    public Task DeleteDiscussionLinkAsync(
        RepoSlug repo,
        int number,
        int targetDiscussionNumber,
        DiscussionRelationshipType relationshipType,
        CancellationToken cancellationToken = default) =>
        SendAsync<object>(
            HttpMethod.Delete,
            $"{OgbApiClientHelpers.BuildDiscussionsPath(repo)}/{number}/links/{targetDiscussionNumber}?relationshipType={relationshipType.ToString().ToLowerInvariant()}",
            body: null,
            cancellationToken);

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        var token = _credentialStore.GetToken(_host);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Not logged in — run `ogb auth login`.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new SessionExpiredException();
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = OgbApiClientHelpers.TryReadErrorDetail(responseBody);
            var message = detail ?? $"Request failed with status {(int)response.StatusCode}.";
            throw new OgbApiException(message, (int)response.StatusCode, detail ?? responseBody);
        }

        if (typeof(T) == typeof(object) || responseBody.Length == 0)
        {
            return default!;
        }

        var result = JsonSerializer.Deserialize<T>(responseBody, JsonOptions);
        return result ?? throw new OgbApiException("Empty response body.", (int)response.StatusCode, responseBody);
    }
}
