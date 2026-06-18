using OpenGitBase.Common.Services;

namespace OpenGitBase.Features.Organization.Tests.Testing;

public sealed class FixedSystemClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
