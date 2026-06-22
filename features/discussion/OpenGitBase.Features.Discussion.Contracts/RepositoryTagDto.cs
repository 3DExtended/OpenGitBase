using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Discussion.Contracts;

public class RepositoryTagDto : ModelBase<RepositoryTagId, Guid>
{
    public Guid RepositoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
