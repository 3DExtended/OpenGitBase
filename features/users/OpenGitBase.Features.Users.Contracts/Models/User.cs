using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Users.Contracts.Models;

public class User : ModelBase<UserId, Guid>
{
    public string Username { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }
}
