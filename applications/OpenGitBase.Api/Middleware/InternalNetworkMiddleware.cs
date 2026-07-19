using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Security;

namespace OpenGitBase.Api.Middleware;

public sealed class InternalNetworkMiddleware
{
    private readonly RequestDelegate _next;
    private readonly InternalNetworkOptions _options;
    private readonly bool _e2eCaptureEmailEnabled;

    public InternalNetworkMiddleware(
        RequestDelegate next,
        IOptions<InternalNetworkOptions> options,
        IConfiguration configuration)
    {
        _next = next;
        _options = options.Value;
        _e2eCaptureEmailEnabled =
            string.Equals(configuration["E2E:CaptureEmail"], "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(configuration["E2E__CaptureEmail"], "true", StringComparison.OrdinalIgnoreCase);
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
        if (_e2eCaptureEmailEnabled
            && value.StartsWith("/internal/e2e", StringComparison.OrdinalIgnoreCase))
        {
            // Host-side E2E runners often reach HAProxy with a public client IP (VPN / remote agent).
            // CaptureEmail endpoints remain gated by E2eController.IsE2eEnabled().
            return false;
        }

        return _options.RestrictedPathPrefixes.Any(prefix =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        );
    }
}
