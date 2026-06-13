using System.ComponentModel.DataAnnotations.Schema;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.RepositoryMember.Entities;

public class RepositoryMemberEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    [ForeignKey(nameof(Repository))]
    public Guid RepositoryId { get; set; } = Guid.Empty;
    public RepositoryEntity? Repository { get; set; }

    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; } = Guid.Empty;

    public UserEntity? User { get; set; }

    public RepositoryRole Role { get; set; } = RepositoryRole.None;
}
