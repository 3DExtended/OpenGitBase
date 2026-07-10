using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenGitBase.Api.Middleware;
using OpenGitBase.Common.Options;

namespace OpenGitBase.Api.Tests.Middleware;

public class InternalNetworkMiddlewareIntegrationTests
{
    [Fact]
    public async Task RestrictedPath_ExternalClientForwardedByTrustedProxy_Returns403()
    {
        var response = await SendAsync("/api/v1/fleet/bootstrap", "203.0.113.10");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RestrictedPath_InternalClientForwardedByTrustedProxy_AllowsRequest()
    {
        var response = await SendAsync("/api/v1/fleet/bootstrap", "10.20.30.40");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task E2ePath_ExternalClientForwardedByTrustedProxy_Returns403()
    {
        var response = await SendAsync("/internal/e2e/emails", "203.0.113.10");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnrestrictedPath_ExternalClientForwardedByTrustedProxy_AllowsRequest()
    {
        var response = await SendAsync("/public/ping", "203.0.113.10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAsync(string path, string forwardedFor)
    {
        var host = await CreateHostAsync();
        try
        {
            var client = host.GetTestClient();
            client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
            return await client.GetAsync(path);
        }
        finally
        {
            host.Dispose();
        }
    }

    private static Task<IHost> CreateHostAsync()
    {
        return new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.Configure<InternalNetworkOptions>(options =>
                        {
                            options.Enabled = true;
                            options.TrustedProxyAddresses = ["127.0.0.1", "::1"];
                        });
                        services.Configure<ForwardedHeadersOptions>(options =>
                        {
                            options.ForwardedHeaders =
                                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                            options.KnownIPNetworks.Clear();
                            options.KnownProxies.Clear();
                            options.KnownProxies.Add(IPAddress.Loopback);
                            options.KnownProxies.Add(IPAddress.IPv6Loopback);
                        });
                    })
                    .Configure(app =>
                    {
                        app.UseForwardedHeaders();
                        app.UseMiddleware<InternalNetworkMiddleware>();
                        app.Run(async context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status200OK;
                            await context.Response.WriteAsync("ok");
                        });
                    });
            })
            .StartAsync();
    }
}
