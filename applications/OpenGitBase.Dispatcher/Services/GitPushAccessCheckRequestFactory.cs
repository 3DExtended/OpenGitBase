using OpenGitBase.Dispatcher.Models;

namespace OpenGitBase.Dispatcher.Services;

public static class GitPushAccessCheckRequestFactory
{
    public static RepositoryAccessCheckRequest Create(
        RepositoryAccessCheckRequest baseRequest,
        IReadOnlyList<GitRefUpdate> refUpdates,
        long packSizeBytes,
        long maxFileBytes
    ) =>
        new()
        {
            PublicKey = baseRequest.PublicKey,
            AccessToken = baseRequest.AccessToken,
            RepositoryPath = baseRequest.RepositoryPath,
            Operation = baseRequest.Operation,
            PackSizeBytes = packSizeBytes,
            MaxFileBytes = maxFileBytes,
            RefUpdates = refUpdates,
        };
}
