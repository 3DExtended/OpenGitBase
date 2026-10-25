using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>Renames one of the user's own passkeys; none when it is not theirs.</summary>
public class RenamePasskeyQuery : IQuery<PasskeySummary, RenamePasskeyQuery>
{
    public UserId UserId { get; set; } = null!;

    public Guid PasskeyId { get; set; }

    public string Name { get; set; } = string.Empty;
}
