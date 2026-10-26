namespace OpenGitBase.Api.Services;

/// <summary>Reads the content of a repository's LFS objects for the web UI (blob view and raw downloads).</summary>
public interface ILfsContentReader
{
    /// <summary>The recorded size of the object, or null when the repository never received it.</summary>
    Task<long?> FindRecordedSizeAsync(Guid repositoryId, string oid, CancellationToken cancellationToken);

    /// <summary>
    /// The object's plaintext as a stream the caller disposes, from the first healthy plaintext node that holds it.
    /// Null when none does; a repair of the repository is queued then.
    /// </summary>
    Task<Stream?> OpenAsync(Guid repositoryId, string oid, CancellationToken cancellationToken);
}
