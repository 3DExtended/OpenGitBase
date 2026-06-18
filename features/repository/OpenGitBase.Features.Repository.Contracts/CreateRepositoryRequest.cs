namespace OpenGitBase.Features.Repository.Contracts;

public sealed record CreateRepositoryRequest(
    string RepositoryName,
    bool IsPrivate,
    string? OrganizationSlug = null
);
