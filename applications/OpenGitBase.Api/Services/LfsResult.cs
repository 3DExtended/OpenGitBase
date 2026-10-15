namespace OpenGitBase.Api.Services;

/// <summary>An HTTP status and the body to send with it, already in the Git LFS wire format.</summary>
public sealed record LfsResult(int StatusCode, object Body);
