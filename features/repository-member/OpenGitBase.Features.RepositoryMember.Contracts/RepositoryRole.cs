using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.RepositoryMember.Contracts;

public enum RepositoryRole
{
    None = 0,
    Reader = 1,
    Writer = 2,
    Admin = 3,
    Owner = 4,
}
