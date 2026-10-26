namespace OpenGitBase.Common.Services;

/// <summary>The client address of the request being handled, as security events record it.</summary>
public interface IRemoteIpAddressAccessor
{
    /// <summary>The client's IP address after trusted forwarded headers; null outside a request.</summary>
    string? RemoteIpAddress { get; }
}
