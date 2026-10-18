namespace OpenGitBase.Features.Repository.Entities;

/// <summary>A Git LFS object whose upload was verified. Only recorded objects are offered for download.</summary>
public class LfsObjectEntity
{
    public Guid RepositoryId { get; set; }

    /// <summary>Lowercase hex SHA-256 of the content.</summary>
    public string Oid { get; set; } = string.Empty;

    public long Size { get; set; }

    /// <summary>Repository key version of the encrypted copies; 0 while the object has none.</summary>
    public int KeyVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public RepositoryEntity? Repository { get; set; }
}
