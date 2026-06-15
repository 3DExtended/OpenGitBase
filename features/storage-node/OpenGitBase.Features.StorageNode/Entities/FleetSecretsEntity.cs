using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.StorageNode.Entities;

public class FleetSecretsEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public string DispatcherSshPublicKey { get; set; } = string.Empty;

    public string DispatcherSshPrivateKeyProtected { get; set; } = string.Empty;

    public string FleetBootstrapTokenHash { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}
