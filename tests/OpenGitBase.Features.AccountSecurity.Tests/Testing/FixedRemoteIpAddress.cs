using OpenGitBase.Common.Services;

namespace OpenGitBase.Features.AccountSecurity.Tests.Testing;

public sealed class FixedRemoteIpAddress : IRemoteIpAddressAccessor
{
    public string? RemoteIpAddress { get; set; } = "203.0.113.7";
}
