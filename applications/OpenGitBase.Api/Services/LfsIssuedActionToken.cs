namespace OpenGitBase.Api.Services;

public sealed record LfsIssuedActionToken(string Token, DateTimeOffset ExpiresAt, int ExpiresInSeconds);
