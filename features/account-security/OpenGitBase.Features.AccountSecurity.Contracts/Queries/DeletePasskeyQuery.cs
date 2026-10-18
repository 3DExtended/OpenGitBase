using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>Deletes one of the user's own passkeys; none when it is not theirs.</summary>
public class DeletePasskeyQuery : IQuery<Unit, DeletePasskeyQuery>
{
    public UserId UserId { get; set; } = null!;

    public Guid PasskeyId { get; set; }
}
