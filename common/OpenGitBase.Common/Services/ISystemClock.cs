namespace OpenGitBase.Common.Services;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}
