namespace OpenGitBase.Api.Models;

using OpenGitBase.Features.Repository.Contracts;

public sealed class RepositoryContentAccessResult
{
    public RepositoryContentAccessResultKind Kind { get; init; }

    public RepositoryDto? Repository { get; init; }

    public static RepositoryContentAccessResult Allow(RepositoryDto repository) =>
        new() { Kind = RepositoryContentAccessResultKind.Allowed, Repository = repository };

    public static RepositoryContentAccessResult NotFound() =>
        new() { Kind = RepositoryContentAccessResultKind.NotFound };

    public static RepositoryContentAccessResult Forbidden() =>
        new() { Kind = RepositoryContentAccessResultKind.Forbidden };

    public static RepositoryContentAccessResult Unavailable() =>
        new() { Kind = RepositoryContentAccessResultKind.Unavailable };
}
