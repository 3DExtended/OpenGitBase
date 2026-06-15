using System.ComponentModel.DataAnnotations;
using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Users.Entities;

public class UserEntity : IIdentifiableEntity<Guid>
{
    [Key]
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string NormalizedUsername { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsAdmin { get; set; }

    public UserCredentialsEntity? UserCredentials { get; set; }
}
