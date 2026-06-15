using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.RepositoryMember.Contracts;

public class RepositoryMemberDto : ModelBase<RepositoryMemberId, Guid>
{
    public RepositoryId RepositoryId { get; set; } = default!;

    public UserId UserId { get; set; } = default!;

    public string? Username { get; set; }

    public RepositoryRole Role { get; set; } = RepositoryRole.None;
}
