using System.Diagnostics;

namespace OpenGitBase.ComputeAgent;

#pragma warning disable SA1202
#pragma warning disable SA1204
#pragma warning disable SA1402

public sealed class FirecrackerLauncher : IFirecrackerLauncher
{
    private readonly ComputeAgentOptions _options;
    private readonly IHostEgressEnforcer _egressEnforcer;
    private readonly ProcessSandboxExecutor _fallback = new();

    public FirecrackerLauncher(ComputeAgentOptions options, IHostEgressEnforcer egressEnforcer)
    {
        _options = options;
        _egressEnforcer = egressEnforcer;
    }

    private static string EscapeShell(string script) => "'" + script.Replace("'", "'\\''") + "'";

    public async Task<FirecrackerLaunchResult> LaunchAsync(
        FirecrackerLaunchRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!CanBootMicroVm(request, out var firecrackerBinary, out var reason))
        {
            return await FallbackAsync(request, reason, cancellationToken).ConfigureAwait(false);
        }

        var start = Stopwatch.GetTimestamp();
        await using var vm = new FirecrackerMicroVm(_options);
        try
        {
            await vm.BootAsync(request, firecrackerBinary!, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(vm.TapInterface))
            {
                await _egressEnforcer
                    .ApplyTapEgressAsync(vm.TapInterface, request.EgressAllowlist, cancellationToken)
                    .ConfigureAwait(false);
            }

            var cwd = request.WorkingDirectory;
            if (string.IsNullOrWhiteSpace(cwd))
            {
                cwd = request.Environment.TryGetValue("CI_PROJECT_DIR", out var projectDir)
                    ? projectDir
                    : "/workspace/repo";
            }

            var client = new VsockGuestClient(vm.VsockUdsPath);
            var execution = await client
                .ExecuteAsync(
                    new VsockGuestExecuteRequest
                    {
                        User = request.RunAsUser,
                        Cwd = cwd,
                        Script = request.Script,
                        Environment = request.Environment,
                    },
                    cancellationToken,
                    request.OnOutputLine
                )
                .ConfigureAwait(false);

            var elapsedMs = (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            return new FirecrackerLaunchResult
            {
                Success = execution.Success,
                ExitCode = execution.ExitCode,
                DurationMs = elapsedMs,
                StdOut = execution.StdOut,
                StdErr = execution.StdErr,
                ExecutorLabel = "FirecrackerMicroVM",
                TapInterface = vm.TapInterface,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var elapsedMs = (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            return new FirecrackerLaunchResult
            {
                Success = false,
                ExitCode = -1,
                DurationMs = elapsedMs,
                StdErr = ex.Message,
                ExecutorLabel = "FirecrackerMicroVM",
            };
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(vm.TapInterface))
            {
                await _egressEnforcer.RemoveTapEgressAsync(vm.TapInterface, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
    }

    public bool CanBootMicroVm(
        FirecrackerLaunchRequest request,
        out string? firecrackerBinary,
        out string? skipReason
    )
    {
        if (!File.Exists("/dev/kvm"))
        {
            firecrackerBinary = null;
            skipReason = "KVM unavailable";
            return false;
        }

        firecrackerBinary = ResolveFirecrackerBinary();
        if (firecrackerBinary is null)
        {
            skipReason = "Firecracker binary missing";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.RootFsPath) || !Directory.Exists(request.RootFsPath))
        {
            skipReason = "OGB_ROOTFS overlay root missing";
            return false;
        }

        if (!File.Exists(_options.KernelPath))
        {
            skipReason = $"Kernel missing at {_options.KernelPath}";
            return false;
        }

        skipReason = null;
        return true;
    }

    private static string? ResolveFirecrackerBinary()
    {
        foreach (var candidate in new[] { "firecracker", "/usr/bin/firecracker", "/usr/local/bin/firecracker" })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private async Task<FirecrackerLaunchResult> FallbackAsync(
        FirecrackerLaunchRequest request,
        string? reason,
        CancellationToken cancellationToken
    )
    {
        var userPrefix = request.RunAsUser.Equals("root", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $"su -s /bin/sh {request.RunAsUser} -c ";
        var wrappedScript = string.IsNullOrEmpty(userPrefix)
            ? request.Script
            : $"{userPrefix}{EscapeShell(request.Script)}";
        var result = await _fallback
            .ExecuteAsync(wrappedScript, request.WorkingDirectory, request.Environment, cancellationToken)
            .ConfigureAwait(false);
        var stderr = result.StdErr;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            stderr = string.IsNullOrWhiteSpace(stderr)
                ? $"firecracker_fallback={reason}"
                : $"{stderr}\nfirecracker_fallback={reason}";
        }

        return new FirecrackerLaunchResult
        {
            Success = result.Success,
            ExitCode = result.ExitCode,
            DurationMs = result.DurationMs,
            StdOut = result.StdOut,
            StdErr = stderr,
            ExecutorLabel = "ProcessSandboxFallback",
        };
    }
}
