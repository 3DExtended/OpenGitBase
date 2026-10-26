namespace OpenGitBase.Api.Services;

/// <summary>
/// An HTTP status and the body to send with it, already in the Git LFS wire format. A 503 may say when to retry.
/// </summary>
public sealed record LfsResult(int StatusCode, object Body)
{
    public int? RetryAfterSeconds { get; init; }
}
