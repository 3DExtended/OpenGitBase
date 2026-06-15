namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class GenerateFleetDispatcherSshKeysResult
{
    public string DispatcherSshPublicKey { get; init; } = string.Empty;

    public string FleetBootstrapToken { get; init; } = string.Empty;
}
