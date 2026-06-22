using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Discussion.Contracts;

public class CreateRepositoryTagQuery : IQuery<RepositoryTagDto, CreateRepositoryTagQuery>
{
    public Guid RepositoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
}
