using System.Net.Http.Json;

namespace OpenGitBase.ComputeAgent;

public sealed class HostEgressEnforcer : IHostEgressEnforcer
{
    public async Task<IReadOnlyList<string>> ResolveAllowlistAsync(
        HttpClient apiClient,
        string runsOn,
        Guid? organizationId,
        CancellationToken cancellationToken
    )
    {
        var url = $"pipeline/egress/effective?runs-on={Uri.EscapeDataString(runsOn)}";
        if (organizationId.HasValue)
        {
            url += $"&organizationId={organizationId.Value:D}";
        }

        var response = await apiClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        return await response.Content
            .ReadFromJsonAsync<IReadOnlyList<string>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? [];
    }

    public Task<HostEgressCheckResult> ValidateDomainAsync(
        string domain,
        IReadOnlyList<string> allowlist,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;
        if (string.IsNullOrWhiteSpace(domain))
        {
            return Task.FromResult(new HostEgressCheckResult { Allowed = true });
        }

        var normalized = domain.Trim().TrimStart('.').ToLowerInvariant();
        var allowed = allowlist.Any(entry =>
            normalized.Equals(entry, StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("." + entry, StringComparison.OrdinalIgnoreCase)
        );
        if (allowed)
        {
            return Task.FromResult(new HostEgressCheckResult { Allowed = true });
        }

        return Task.FromResult(
            new HostEgressCheckResult
            {
                Allowed = false,
                DenialLogLine =
                    $"Egress denied for domain '{domain}'. Submit a Domain Allowance Request with justification to allow this host.",
            }
        );
    }
}
