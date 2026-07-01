namespace OpenGitBase.Common.SendGrid;

public sealed class CapturedEmailMessage
{
    public string To { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string HtmlBody { get; init; } = string.Empty;

    public DateTimeOffset SentAt { get; init; } = DateTimeOffset.UtcNow;
}
