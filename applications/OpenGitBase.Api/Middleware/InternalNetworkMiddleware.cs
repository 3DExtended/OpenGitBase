using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Security;

namespace OpenGitBase.Api.Middleware;

public sealed class InternalNetworkMiddleware
{
    private readonly RequestDelegate _next;
    private readonly InternalNetworkOptions _options;

    public InternalNetworkMiddleware(RequestDelegate next, IOptions<InternalNetworkOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled || !ShouldRestrict(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is null || !InternalNetworkAddress.IsInternal(remoteIp))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response
                .WriteAsJsonAsync(new { error = "This endpoint is only available on internal networks." })
                .ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private bool ShouldRestrict(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return _options.RestrictedPathPrefixes.Any(prefix =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        );
    }
}
