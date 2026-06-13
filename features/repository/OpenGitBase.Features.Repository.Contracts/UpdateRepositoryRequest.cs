namespace OpenGitBase.Features.Repository.Contracts;

public sealed record UpdateRepositoryRequest(string Name, bool IsPrivate);
