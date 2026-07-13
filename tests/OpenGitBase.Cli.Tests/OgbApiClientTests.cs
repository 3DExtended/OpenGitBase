using System.Net;
using System.Net.Http.Headers;
using OpenGitBase.Cli.Api;
using OpenGitBase.Cli.Api.Models;
using OpenGitBase.Cli.Auth;
using OpenGitBase.Cli.Git;
using OpenGitBase.Cli.Tests.TestSupport;

namespace OpenGitBase.Cli.Tests;

public sealed class OgbApiClientTests
{
    [Fact]
    public async Task Sends_bearer_token_and_create_payload()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.Created,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":7,"title":"Bug","status":0,"createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","tags":[]}
            """);

        var store = new InMemoryCredentialStore();
        store.SaveToken("https://forge.example.com", "jwt-token");
        var client = new OgbApiClient(
            new HttpClient(handler),
            store,
            "https://forge.example.com",
            "https://forge.example.com/api");

        var discussion = await client.CreateDiscussionAsync(
            new RepoSlug { Owner = "acme", Slug = "demo" },
            new CreateDiscussionRequest { Title = "Bug", Body = "Details" }).ConfigureAwait(false);

        Assert.Equal(7, discussion.Number);
        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains("/repository/by-slug/acme/demo/discussions", request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("jwt-token", request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task Deserializes_identifier_wrapped_id_from_api()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"id":{"value":"11111111-1111-1111-1111-111111111111"},"number":1,"title":"Bug","status":"Open","createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","tags":[]}
            """);

        var store = new InMemoryCredentialStore();
        store.SaveToken("https://forge.example.com", "jwt-token");
        var client = new OgbApiClient(
            new HttpClient(handler),
            store,
            "https://forge.example.com",
            "https://forge.example.com/api");

        var discussion = await client.GetDiscussionAsync(
            new RepoSlug { Owner = "acme", Slug = "demo" },
            1,
            includeComments: false).ConfigureAwait(false);

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), discussion.Id);
        Assert.Equal(DiscussionStatus.Open, discussion.Status);
    }

    [Fact]
    public async Task Reuses_http_client_with_existing_base_address()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "[]");
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"id":{"value":"11111111-1111-1111-1111-111111111111"},"number":1,"title":"Bug","status":"Open","createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","tags":[]}
            """);

        var store = new InMemoryCredentialStore();
        store.SaveToken("https://forge.example.com", "jwt-token");
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://forge.example.com/api/"),
        };

        var listClient = new OgbApiClient(httpClient, store, "https://forge.example.com", "https://forge.example.com/api");
        var discussions = await listClient.ListDiscussionsAsync(
            new RepoSlug { Owner = "acme", Slug = "demo" },
            status: null).ConfigureAwait(false);

        var getClient = new OgbApiClient(httpClient, store, "https://forge.example.com", "https://forge.example.com/api");
        var discussion = await getClient.GetDiscussionAsync(
            new RepoSlug { Owner = "acme", Slug = "demo" },
            1,
            includeComments: false).ConfigureAwait(false);

        Assert.Empty(discussions);
        Assert.Equal(1, discussion.Number);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Unauthorized_maps_to_session_expired()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.Unauthorized, """{"error":"Sign in required."}""");

        var store = new InMemoryCredentialStore();
        store.SaveToken("https://forge.example.com", "jwt-token");
        var client = new OgbApiClient(
            new HttpClient(handler),
            store,
            "https://forge.example.com",
            "https://forge.example.com/api");

        await Assert.ThrowsAsync<SessionExpiredException>(() =>
            client.GetDiscussionAsync(
                new RepoSlug { Owner = "acme", Slug = "demo" },
                1,
                includeComments: false)).ConfigureAwait(false);
    }
}
