using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.PublicGitSshKey.Contracts;

public class DeleteUserSshKeysQuery : IQuery<Unit, DeleteUserSshKeysQuery>
{
    public UserId UserId { get; set; } = default!;
}
