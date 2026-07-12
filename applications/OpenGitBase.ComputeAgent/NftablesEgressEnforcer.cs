using System.Diagnostics;
using System.Net.Http.Json;

namespace OpenGitBase.ComputeAgent;

public sealed class NftablesEgressEnforcer : IHostEgressEnforcer
{
    private readonly HostEgressEnforcer _allowlistResolver = new();

    public Task<IReadOnlyList<string>> ResolveAllowlistAsync(
        HttpClient apiClient,
        string runsOn,
        Guid? organizationId,
        CancellationToken cancellationToken
    ) =>
        _allowlistResolver.ResolveAllowlistAsync(apiClient, runsOn, organizationId, cancellationToken);

    public Task<HostEgressCheckResult> ValidateDomainAsync(
        string domain,
        IReadOnlyList<string> allowlist,
        CancellationToken cancellationToken
    ) =>
        _allowlistResolver.ValidateDomainAsync(domain, allowlist, cancellationToken);

    public async Task ApplyTapRulesAsync(
        string tapInterface,
        IReadOnlyList<string> allowlist,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(tapInterface))
        {
            return;
        }

        var tableName = $"ogb_{tapInterface.Replace('-', '_')}";
        var setName = $"{tableName}_allow";
        await RunShellAsync($"nft delete table inet {tableName} 2>/dev/null || true", cancellationToken)
            .ConfigureAwait(false);
        await RunShellAsync($"nft add table inet {tableName}", cancellationToken).ConfigureAwait(false);
        await RunShellAsync(
            $"nft add chain inet {tableName} forward {{ type filter hook forward priority 0\\; policy drop\\; }}",
            cancellationToken
        ).ConfigureAwait(false);
        await RunShellAsync($"nft add set inet {tableName} {setName} {{ type ipv4_addr\\; flags timeout\\; }}", cancellationToken)
            .ConfigureAwait(false);
        foreach (var domain in allowlist)
        {
            foreach (var address in await ResolveDomainAddressesAsync(domain, cancellationToken).ConfigureAwait(false))
            {
                await RunShellAsync(
                    $"nft add element inet {tableName} {setName} {{ {address} timeout 300s }}",
                    cancellationToken
                ).ConfigureAwait(false);
            }
        }

        await RunShellAsync(
            $"nft add rule inet {tableName} forward iifname \"{tapInterface}\" ip daddr @{setName} accept",
            cancellationToken
        ).ConfigureAwait(false);
        await RunShellAsync(
            $"nft add rule inet {tableName} forward iifname \"{tapInterface}\" log prefix \"ogb-egress-deny: \" drop",
            cancellationToken
        ).ConfigureAwait(false);
    }

    public Task RemoveTapRulesAsync(string tapInterface, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tapInterface))
        {
            return Task.CompletedTask;
        }

        var tableName = $"ogb_{tapInterface.Replace('-', '_')}";
        return RunShellAsync($"nft delete table inet {tableName} 2>/dev/null || true", cancellationToken);
    }

    public string BuildDenialLogLine(string domain) =>
        $"Egress denied for domain '{domain}' on tap interface. Submit a Domain Allowance Request with justification to allow this host.";

    public Task ApplyTapEgressAsync(
        string tapInterface,
        IReadOnlyList<string> allowlist,
        CancellationToken cancellationToken
    ) => ApplyTapRulesAsync(tapInterface, allowlist, cancellationToken);

    public Task RemoveTapEgressAsync(string tapInterface, CancellationToken cancellationToken) =>
        RemoveTapRulesAsync(tapInterface, cancellationToken);

    private static async Task<IReadOnlyList<string>> ResolveDomainAddressesAsync(
        string domain,
        CancellationToken cancellationToken
    )
    {
        var info = new ProcessStartInfo("getent")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("hosts");
        info.ArgumentList.Add(domain);
        using var process = Process.Start(info);
        if (process is null)
        {
            return [];
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.Ordinal)
            .Select(address => address!)
            .ToList();
    }

    private static async Task RunShellAsync(string command, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("sh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(command);
        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Failed to run: {command}");
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }
}
