using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;

namespace OpenGitBase.Api.Tests.Integration;

/// <summary>
/// The post-receive hook relays the whole encrypted repository bundle, hex-encoded (2x), to
/// replicate-artifact. Kestrel's default 30,000,000-byte body limit rejected it with 413 for
/// every repository whose bundle passed ~15 MB, and the encrypted quorum silently stopped
/// receiving pushes. TestServer does not enforce Kestrel's limit, so this runs on real Kestrel.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ReplicateArtifactRequestSizeTests
{
    private const int KestrelDefaultMaxRequestBodySize = 30_000_000;

    [Fact]
    public async Task ReplicateArtifact_BodyAboveKestrelDefault_ReachesTheController()
    {
        using var connection = SqliteTestConnection.OpenInMemory();
        await using var factory = new WebApplicationFactory<ApiEntryPoint>().WithWebHostBuilder(
            builder =>
            {
                builder.UseEnvironment("E2ETest");
                builder.UseSetting(WebHostDefaults.ServerUrlsKey, "http://127.0.0.1:0");
                builder.ConfigureServices(services =>
                {
                    AuthTestServerConfiguration.ConfigureDatabase(services, connection);
                    AuthTestServerConfiguration.ConfigureOptions(services);
                    AuthTestServerConfiguration.ConfigureGoogleValidator(services);
                });
            }
        );
        factory.UseKestrel();
        factory.StartServer();
        using (var scope = factory.Services.CreateScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await using var context = await contextFactory.CreateDbContextAsync();
            await context.Database.EnsureCreatedAsync();
        }

        using var client = factory.CreateClient();
        // ~40 MB: a 20 MB bundle as hex, the size that broke admin/notes on 2026-10-09.
        var body = BuildRequestBody(hexLength: 40_000_000);
        Assert.True(body.Length > KestrelDefaultMaxRequestBodySize);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/storage-nodes/repositories/{Guid.NewGuid()}/replicate-artifact"
        )
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/json"
        );
        // Wait for 100-continue so a rejection arrives as a status code, not a broken pipe.
        request.Headers.ExpectContinue = true;
        var response = await client.SendAsync(request);

        // No bearer token: the controller answers 401. Before the fix Kestrel answered 413
        // before the controller ever ran.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void ReplicateArtifact_KeepsABoundedBodyLimit()
    {
        var action = typeof(StorageReplicationController).GetMethod(
            nameof(StorageReplicationController.ReplicateArtifact)
        )!;

        Assert.Null(action.GetCustomAttribute<DisableRequestSizeLimitAttribute>());
        var limit = action.GetCustomAttribute<RequestSizeLimitAttribute>();
        Assert.NotNull(limit);
        Assert.Equal(
            StorageReplicationController.ReplicateArtifactMaxRequestBodyBytes,
            256L * 1024 * 1024
        );
    }

    private static byte[] BuildRequestBody(int hexLength)
    {
        var builder = new StringBuilder(hexLength + 200);
        builder.Append("{\"targetStorageNodeId\":\"");
        builder.Append(Guid.NewGuid());
        builder.Append("\",\"watermark\":1,\"manifest\":{},\"bundleBase64\":\"");
        builder.Append('0', hexLength);
        builder.Append("\"}");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }
}
