namespace OpenGitBase.ComputeAgent;

public interface IHostEgressEnforcer
{
    Task<IReadOnlyList<string>> ResolveAllowlistAsync(
        HttpClient apiClient,
        string runsOn,
        Guid? organizationId,
        CancellationToken cancellationToken
    );

    Task<HostEgressCheckResult> ValidateDomainAsync(
        string domain,
        IReadOnlyList<string> allowlist,
        CancellationToken cancellationToken
    );

    Task ApplyTapEgressAsync(
        string tapInterface,
        IReadOnlyList<string> allowlist,
        CancellationToken cancellationToken
    );

    Task RemoveTapEgressAsync(string tapInterface, CancellationToken cancellationToken);
}
