namespace OpenGitBase.Features.Repository.Contracts;

public sealed class DeleteRepositoryWithStorageResult
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public static DeleteRepositoryWithStorageResult Deleted() => new() { Success = true };

    public static DeleteRepositoryWithStorageResult Failed(string error) =>
        new() { Success = false, Error = error };
}
