namespace OpenGitBase.Dispatcher.Models;

/// <summary>
/// Status and body of an API LFS call; the body is already in the LFS wire format. A 503 may carry the seconds
/// after which the client should retry.
/// </summary>
public sealed record LfsApiAnswer(int StatusCode, string Body, int? RetryAfterSeconds = null);
