using System.Net;
using System.Text;
using System.Text.Json;
using OpenGitBase.Api.Models.StorageContent;
using OpenGitBase.Api.Services;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Tests.Services;

public class StorageContentClientTests
{
    [Fact]
    public async Task GetDiffAsync_WhenSuccessful_ReturnsDiffPayload()
    {
        const string responseJson =
            """
            {
              "baseSha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "headSha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
              "files": [
                {
                  "oldPath": null,
                  "newPath": "feature.txt",
                  "status": "added",
                  "isBinary": false,
                  "hunks": [
                    {
                      "oldStart": 0,
                      "oldLines": 0,
                      "newStart": 1,
                      "newLines": 1,
                      "lines": [
                        {
                          "type": "add",
                          "content": "feature",
                          "oldLineNumber": null,
                          "newLineNumber": 1
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var handler = new StubHttpMessageHandler(
            (request, _) =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Contains("/internal/repos/content/diff?", request.RequestUri?.ToString());
                Assert.Contains("baseSha=aaaaaaaa", request.RequestUri?.ToString());
                Assert.Contains("headSha=bbbbbbbb", request.RequestUri?.ToString());
                Assert.Equal("Bearer secret-token", request.Headers.Authorization?.ToString());
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
                    }
                );
            }
        );

        var client = new StorageContentClient(new HttpClient(handler));
        var result = await client.GetDiffAsync(
            CreateTarget(),
            "secret-token",
            "/srv/git/repo.git",
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Single(result!.Files);
        Assert.Equal("added", result.Files[0].Status);
        Assert.Equal("feature.txt", result.Files[0].NewPath);
    }

    [Fact]
    public async Task GetTreeAsync_ReadsEachEntrysLastModifiedDate()
    {
        const string responseJson =
            """
            {
              "ref": "main",
              "path": "",
              "entries": [
                {
                  "name": "README.md",
                  "path": "README.md",
                  "type": "blob",
                  "size": 12,
                  "lastModifiedAt": "2025-02-03T04:05:06Z"
                },
                {
                  "name": "src",
                  "path": "src",
                  "type": "tree",
                  "size": null,
                  "lastModifiedAt": null
                }
              ]
            }
            """;

        var handler = new StubHttpMessageHandler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
                    }
                )
        );

        var client = new StorageContentClient(new HttpClient(handler));
        var result = await client.GetTreeAsync(
            CreateTarget(),
            "secret-token",
            "/srv/git/repo.git",
            "main",
            string.Empty,
            CancellationToken.None
        );

        Assert.False(result.IsMissing);
        var tree = Assert.IsType<StorageContentTreePayload>(result.Payload);
        Assert.Equal(
            new DateTimeOffset(2025, 2, 3, 4, 5, 6, TimeSpan.Zero),
            tree.Entries[0].LastModifiedAt
        );
        Assert.Null(tree.Entries[1].LastModifiedAt);
    }

    // As for files: only "not_found" says the directory is missing. A revision the node lacks
    // (a replica that has not caught up yet) or any other failure leaves IsMissing false.
    [Theory]
    [InlineData(HttpStatusCode.NotFound, """{ "error": "Path is not a directory.", "code": "not_found" }""", true)]
    [InlineData(HttpStatusCode.NotFound, """{ "error": "Unknown revision.", "code": "invalid_ref" }""", false)]
    [InlineData(HttpStatusCode.NotFound, """{ "error": "Repository not found." }""", false)]
    [InlineData(HttpStatusCode.InternalServerError, """{ "error": "boom", "code": "git_error" }""", false)]
    [InlineData(HttpStatusCode.BadGateway, "<html>bad gateway</html>", false)]
    public async Task GetTreeAsync_WhenStorageFails_ReportsMissingOnlyForNotFound(
        HttpStatusCode status,
        string body,
        bool expectedMissing
    )
    {
        var handler = new StubHttpMessageHandler(
            (request, _) =>
            {
                Assert.Contains("/internal/repos/content/tree?", request.RequestUri?.ToString());
                Assert.Contains("path=src%2Fmissing", request.RequestUri?.ToString());
                return Task.FromResult(
                    new HttpResponseMessage(status)
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json"),
                    }
                );
            }
        );

        var client = new StorageContentClient(new HttpClient(handler));
        var result = await client.GetTreeAsync(
            CreateTarget(),
            "secret-token",
            "/srv/git/repo.git",
            "main",
            "src/missing",
            CancellationToken.None
        );

        Assert.Null(result.Payload);
        Assert.Equal(expectedMissing, result.IsMissing);
    }

    [Fact]
    public async Task CheckMergeabilityAsync_WhenSuccessful_ReturnsStatus()
    {
        const string responseJson =
            """
            {
              "status": "mergeable",
              "canFastForward": true,
              "alreadyUpToDate": false
            }
            """;

        var handler = new StubHttpMessageHandler(
            (request, _) =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Contains("/internal/repos/content/mergeability?", request.RequestUri?.ToString());
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
                    }
                );
            }
        );

        var client = new StorageContentClient(new HttpClient(handler));
        var result = await client.CheckMergeabilityAsync(
            CreateTarget(),
            "secret-token",
            "/srv/git/repo.git",
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Equal("mergeable", result!.Status);
        Assert.True(result.CanFastForward);
    }

    [Fact]
    public async Task ExecuteMergeAsync_WhenSuccessful_ReturnsCommitSha()
    {
        string? requestBody = null;
        var handler = new StubHttpMessageHandler(
            async (request, _) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Contains("/internal/repos/content/merge?", request.RequestUri?.ToString());
                requestBody = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "commitSha": "cccccccccccccccccccccccccccccccccccccccc",
                          "strategy": "merge_commit",
                          "targetRef": "refs/heads/main"
                        }
                        """,
                        Encoding.UTF8,
                        "application/json"
                    ),
                };
            }
        );

        var client = new StorageContentClient(new HttpClient(handler));
        var result = await client.ExecuteMergeAsync(
            CreateTarget(),
            "secret-token",
            "/srv/git/repo.git",
            new StorageContentExecuteMergeRequest
            {
                TargetRef = "main",
                SourceRef = "feature",
                Strategy = "merge_commit",
                CommitMessage = "Merge feature",
            },
            CancellationToken.None
        );

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("cccccccccccccccccccccccccccccccccccccccc", result.CommitSha);
        Assert.Equal("merge_commit", result.Strategy);
        Assert.Contains("\"TargetRef\":\"main\"", requestBody);
        Assert.Contains("\"Strategy\":\"merge_commit\"", requestBody);
    }

    [Fact]
    public async Task ExecuteMergeAsync_WhenConflict_ReturnsFailureDetails()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.Conflict)
                    {
                        Content = new StringContent(
                            """
                            {
                              "error": "Merge has conflicts.",
                              "code": "conflicts"
                            }
                            """,
                            Encoding.UTF8,
                            "application/json"
                        ),
                    }
                )
        );

        var client = new StorageContentClient(new HttpClient(handler));
        var result = await client.ExecuteMergeAsync(
            CreateTarget(),
            "secret-token",
            "/srv/git/repo.git",
            new StorageContentExecuteMergeRequest
            {
                TargetRef = "main",
                SourceRef = "feature",
                Strategy = "merge_commit",
            },
            CancellationToken.None
        );

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("conflicts", result.ErrorCode);
        Assert.Equal("Merge has conflicts.", result.ErrorMessage);
    }

    [Fact]
    public async Task GetBlobAsync_WhenSuccessful_ReturnsPayload()
    {
        var handler = new StubHttpMessageHandler(
            (request, _) =>
            {
                Assert.Contains("/internal/repos/content/blob?", request.RequestUri?.ToString());
                Assert.Contains("path=src%2Fapp.ts", request.RequestUri?.ToString());
                Assert.Equal("Bearer secret-token", request.Headers.Authorization?.ToString());
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            """{ "ref": "main", "path": "src/app.ts", "size": 5, "textContent": "hello" }""",
                            Encoding.UTF8,
                            "application/json"
                        ),
                    }
                );
            }
        );

        var client = new StorageContentClient(new HttpClient(handler));
        var result = await client.GetBlobAsync(
            CreateTarget(),
            "secret-token",
            "/srv/git/repo.git",
            "main",
            "src/app.ts",
            CancellationToken.None
        );

        Assert.False(result.IsMissing);
        Assert.Equal("hello", result.Payload?.TextContent);
    }

    // A storage node also answers 404 for a revision it lacks, e.g. a replica that has not caught
    // up yet, and for a repository it does not hold. Only "not_found" says the file is missing.
    [Theory]
    [InlineData(HttpStatusCode.NotFound, """{ "error": "path 'x' does not exist in 'main'", "code": "not_found" }""", true)]
    [InlineData(HttpStatusCode.NotFound, """{ "error": "Unknown revision.", "code": "invalid_ref" }""", false)]
    [InlineData(HttpStatusCode.NotFound, """{ "error": "Repository not found." }""", false)]
    [InlineData(HttpStatusCode.InternalServerError, """{ "error": "boom", "code": "git_error" }""", false)]
    [InlineData(HttpStatusCode.BadGateway, "<html>bad gateway</html>", false)]
    public async Task GetBlobAsync_WhenStorageFails_ReportsMissingOnlyForNotFound(
        HttpStatusCode status,
        string body,
        bool expectedMissing
    )
    {
        var handler = new StubHttpMessageHandler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(status)
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json"),
                    }
                )
        );

        var client = new StorageContentClient(new HttpClient(handler));
        var result = await client.GetBlobAsync(
            CreateTarget(),
            "secret-token",
            "/srv/git/repo.git",
            "main",
            "x",
            CancellationToken.None
        );

        Assert.Null(result.Payload);
        Assert.Equal(expectedMissing, result.IsMissing);
    }

    private static RepositoryRoutingTargetDto CreateTarget() =>
        new()
        {
            InternalHost = "storage-1",
            InternalHttpPort = 8081,
            IsPrimary = true,
        };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler
        )
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => _handler(request, cancellationToken);
    }
}
