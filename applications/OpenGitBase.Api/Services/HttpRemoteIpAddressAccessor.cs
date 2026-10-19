using OpenGitBase.Common.Services;

namespace OpenGitBase.Api.Services;

/// <summary>
/// The current request's client address. Forwarded headers are applied first, so behind the trusted
/// proxies this is the browser's address rather than the load balancer's.
/// </summary>
public sealed class HttpRemoteIpAddressAccessor : IRemoteIpAddressAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpRemoteIpAddressAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? RemoteIpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
