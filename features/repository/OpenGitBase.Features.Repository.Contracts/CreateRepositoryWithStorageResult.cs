namespace OpenGitBase.Features.Repository.Contracts;

public sealed class CreateRepositoryWithStorageResult
{
    public RepositoryId? RepositoryId { get; init; }

    public string? Error { get; init; }

    public static CreateRepositoryWithStorageResult Created(RepositoryId repositoryId) =>
        new() { RepositoryId = repositoryId };

    public static CreateRepositoryWithStorageResult Failed(string error) =>
        new() { Error = error };
}
