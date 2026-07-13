using System.Net;
using OpenGitBase.Cli.Api;
using OpenGitBase.Cli.Api.Models;
using OpenGitBase.Cli.Auth;
using OpenGitBase.Cli.Git;
using OpenGitBase.Cli.Tests.TestSupport;

namespace OpenGitBase.Cli.Tests;

public sealed class OgbApiClientMrTests
{
    [Fact]
    public async Task ListMergeRequests_sends_status_query_param()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "[]");

        var store = new InMemoryCredentialStore();
        store.SaveToken("https://forge.example.com", "jwt-token");
        var client = new OgbApiClient(
            new HttpClient(handler),
            store,
            "https://forge.example.com",
            "https://forge.example.com/api");

        await client.ListMergeRequestsAsync(
            new RepoSlug { Owner = "acme", Slug = "demo" },
            MergeRequestStatus.Open).ConfigureAwait(false);

        var request = handler.Requests.Single();
        Assert.Contains("/merge-requests?status=open", request.RequestUri!.PathAndQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMergeRequest_posts_payload()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.Created,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":3,"title":"Feature","status":1,"isDraft":false,"sourceRef":"feature","targetRef":"main","sourceHeadSha":"abc","targetBaseSha":"def","createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","approvalCountAtHead":0,"requiredApprovalCount":1}
            """);

        var store = new InMemoryCredentialStore();
        store.SaveToken("https://forge.example.com", "jwt-token");
        var client = new OgbApiClient(
            new HttpClient(handler),
            store,
            "https://forge.example.com",
            "https://forge.example.com/api");

        var mergeRequest = await client.CreateMergeRequestAsync(
            new RepoSlug { Owner = "acme", Slug = "demo" },
            new CreateMergeRequestRequest
            {
                Title = "Feature",
                SourceRef = "feature",
                TargetRef = "main",
            }).ConfigureAwait(false);

        Assert.Equal(3, mergeRequest.Number);
        Assert.Equal(HttpMethod.Post, handler.Requests.Single().Method);
    }

    [Fact]
    public async Task MergeMergeRequest_posts_strategy_and_delete_branch()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":3,"title":"Feature","status":3,"isDraft":false,"sourceRef":"feature","targetRef":"main","sourceHeadSha":"abc","targetBaseSha":"def","mergeCommitSha":"merged","createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","approvalCountAtHead":1,"requiredApprovalCount":1}
            """);

        var store = new InMemoryCredentialStore();
        store.SaveToken("https://forge.example.com", "jwt-token");
        var client = new OgbApiClient(
            new HttpClient(handler),
            store,
            "https://forge.example.com",
            "https://forge.example.com/api");

        await client.MergeMergeRequestAsync(
            new RepoSlug { Owner = "acme", Slug = "demo" },
            3,
            new MergeMergeRequestRequest
            {
                Strategy = MergeRequestMergeStrategy.Squash,
                DeleteSourceBranch = true,
            }).ConfigureAwait(false);

        Assert.Contains("/merge-requests/3/merge", handler.Requests.Single().RequestUri!.AbsolutePath, StringComparison.Ordinal);
    }
}
