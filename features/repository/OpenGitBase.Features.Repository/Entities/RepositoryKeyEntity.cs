using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Repository.Entities;

public class RepositoryKeyEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public Guid RepositoryId { get; set; }

    public string KeyCiphertext { get; set; } = string.Empty;

    public int KeyVersion { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public RepositoryEntity? Repository { get; set; }
}
