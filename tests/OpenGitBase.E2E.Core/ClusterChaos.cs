namespace OpenGitBase.E2E.Core;

public interface IClusterChaos
{
    Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default);

    Task StartServiceAsync(string serviceName, CancellationToken cancellationToken = default);

    Task RestoreAllAsync(CancellationToken cancellationToken = default);
}

public sealed class ClusterChaos : IClusterChaos
{
    private static readonly Dictionary<string, string> ServiceToContainer = new(StringComparer.OrdinalIgnoreCase)
    {
        ["storage-1"] = "opengitbase_storage_1",
        ["storage-2"] = "opengitbase_storage_2",
        ["storage-3"] = "opengitbase_storage_3",
    };

    private readonly IOperationTranscript _transcript;
    private readonly HashSet<string> _stoppedServices = new(StringComparer.OrdinalIgnoreCase);

    public ClusterChaos(IOperationTranscript transcript)
    {
        _transcript = transcript;
    }

    public async Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        await RunDockerAsync(["stop", ResolveContainer(serviceName)], cancellationToken).ConfigureAwait(false);
        _stoppedServices.Add(serviceName);
        _transcript.RecordWire(new WireEvent
        {
            Kind = WireEventKind.ClusterAction,
            Summary = $"Stopped container for '{serviceName}'",
        });
    }

    public async Task StartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        await RunDockerAsync(["start", ResolveContainer(serviceName)], cancellationToken).ConfigureAwait(false);
        _stoppedServices.Remove(serviceName);
        _transcript.RecordWire(new WireEvent
        {
            Kind = WireEventKind.ClusterAction,
            Summary = $"Started container for '{serviceName}'",
        });
    }

    public async Task RestoreAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var service in _stoppedServices.ToList())
        {
            await StartServiceAsync(service, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string ResolveContainer(string serviceName) =>
        ServiceToContainer.TryGetValue(serviceName, out var container)
            ? container
            : serviceName;

    private static async Task RunDockerAsync(string[] args, CancellationToken cancellationToken)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "docker",
            WorkingDirectory = E2eEnvironment.RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start docker.");
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"docker {string.Join(' ', args)} failed: {stderr}");
        }
    }
}
