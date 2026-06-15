using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.RepositoryMember.Contracts;

public class DeleteUserRepositoryMembershipsQuery
    : IQuery<Unit, DeleteUserRepositoryMembershipsQuery>
{
    public UserId UserId { get; set; } = default!;
}
