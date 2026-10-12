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

// Production path: client -> Cloudflare -> cloudflared container -> HAProxy (option forwardfor) -> API.
// The API's peer is HAProxy and X-Forwarded-For ends with the cloudflared container's address.
public class ForwardedHeadersSetupTests
{
    private static readonly IPAddress HaproxyAddress = IPAddress.Parse("172.18.0.3");

    [Fact]
    public async Task ClientBehindInternalProxyHops_IsResolvedToTheClientAddress()
    {
        var response = await SendAsync("/whoami", "203.0.113.10, 172.18.0.7");

        Assert.Equal("203.0.113.10", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ClientBehindInternalProxyHops_IsRefusedOnInternalPaths()
    {
        var response = await SendAsync("/api/v1/fleet/bootstrap", "203.0.113.10, 172.18.0.7");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SpoofedInternalAddressInFrontOfTheClient_IsIgnored()
    {
        var response = await SendAsync("/api/v1/fleet/bootstrap", "10.0.0.1, 203.0.113.10, 172.18.0.7");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InternalCallerBehindInternalProxyHops_IsAllowedOnInternalPaths()
    {
        var response = await SendAsync("/api/v1/fleet/bootstrap", "172.18.0.9, 172.18.0.7");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAsync(string path, string forwardedFor)
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.Configure<InternalNetworkOptions>(_ => { });
                        services.Configure<ForwardedHeadersOptions>(options =>
                            ForwardedHeadersSetup.Configure(options, new InternalNetworkOptions())
                        );
                    })
                    .Configure(app =>
                    {
                        app.Use(
                            (context, next) =>
                            {
                                context.Connection.RemoteIpAddress = HaproxyAddress;
                                return next(context);
                            }
                        );
                        app.UseForwardedHeaders();
                        app.UseMiddleware<InternalNetworkMiddleware>();
                        app.Run(context =>
                            context.Response.WriteAsync(
                                context.Connection.RemoteIpAddress?.ToString() ?? string.Empty
                            )
                        );
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        var response = await client.GetAsync(path);
        await response.Content.LoadIntoBufferAsync();
        return response;
    }
}
