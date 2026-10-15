namespace OpenGitBase.Dispatcher.Models;

/// <summary>Status and body of an API LFS call; the body is already in the LFS wire format.</summary>
public sealed record LfsApiAnswer(int StatusCode, string Body);
