using System.Diagnostics;

namespace OpenGitBase.ComputeAgent;

#pragma warning disable SA1204

public sealed class FirecrackerMicroVm : IAsyncDisposable
{
    private readonly ComputeAgentOptions _options;
    private Process? _firecrackerProcess;
    private FirecrackerApiClient? _apiClient;
    private string? _tapName;
    private string? _virtioFsSocket;
    private Process? _virtioFsProcess;

    public FirecrackerMicroVm(ComputeAgentOptions options)
    {
        _options = options;
    }

    public string ApiSocketPath { get; private set; } = string.Empty;

    public string VsockUdsPath { get; private set; } = string.Empty;

    public string? TapInterface => _tapName;

    public async Task BootAsync(
        FirecrackerLaunchRequest request,
        string firecrackerBinary,
        CancellationToken cancellationToken
    )
    {
        var workDir = Path.Combine(Path.GetTempPath(), $"ogb-fc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        ApiSocketPath = Path.Combine(workDir, "fc.sock");
        VsockUdsPath = Path.Combine(workDir, "vsock.sock");

        var rootFs = request.RootFsPath;
        if (string.IsNullOrWhiteSpace(rootFs) || !Directory.Exists(rootFs))
        {
            throw new InvalidOperationException("OGB_ROOTFS overlay root is required for Firecracker boot.");
        }

        var kernelPath = _options.KernelPath;
        if (!File.Exists(kernelPath))
        {
            throw new InvalidOperationException($"Firecracker kernel not found at {kernelPath}.");
        }

        _tapName = await CreateTapAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(request.WorkspaceSharePath))
        {
            _virtioFsSocket = Path.Combine(workDir, "virtiofs.sock");
            await StartVirtioFsAsync(request.WorkspaceSharePath, _virtioFsSocket, cancellationToken)
                .ConfigureAwait(false);
        }

        var bootArgs =
            "console=ttyS0 reboot=k panic=1 pci=off init=/usr/local/bin/ogb-guest-agent rw";
        if (!string.IsNullOrWhiteSpace(request.WorkspaceSharePath))
        {
            bootArgs += " OGB_VIRTIOFS=1";
        }

        _firecrackerProcess = Process.Start(
            new ProcessStartInfo(firecrackerBinary)
            {
                ArgumentList = { "--api-sock", ApiSocketPath },
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            }
        );
        if (_firecrackerProcess is null)
        {
            throw new InvalidOperationException("Failed to start Firecracker process.");
        }

        await WaitForSocketAsync(ApiSocketPath, cancellationToken).ConfigureAwait(false);
        _apiClient = new FirecrackerApiClient(ApiSocketPath);

        await _apiClient
            .PutAsync(
                "boot-source",
                new
                {
                    kernel_image_path = kernelPath,
                    boot_args = bootArgs,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        await _apiClient
            .PutAsync(
                "drives/rootfs",
                new
                {
                    drive_id = "rootfs",
                    path_on_host = rootFs,
                    is_root_device = true,
                    is_read_only = false,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        await _apiClient
            .PutAsync(
                "machine-config",
                new
                {
                    vcpu_count = request.ResourceLimits.CpuLimit,
                    mem_size_mib = request.ResourceLimits.MemoryMiB,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        await _apiClient
            .PutAsync(
                "network-interfaces/eth0",
                new
                {
                    iface_id = "eth0",
                    guest_mac = GenerateGuestMac(),
                    host_dev_name = _tapName,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        await _apiClient
            .PutAsync(
                "vsock",
                new
                {
                    guest_cid = _options.GuestCid,
                    uds_path = VsockUdsPath,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (_virtioFsSocket is not null)
        {
            await _apiClient
                .PutAsync(
                    "fs/workspace",
                    new
                    {
                        guest_mount_id = "workspace",
                        host_fs_device_path = _virtioFsSocket,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        await _apiClient.StartInstanceAsync(cancellationToken).ConfigureAwait(false);
        await WaitForGuestAgentAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _apiClient?.Dispose();
        if (_firecrackerProcess is { HasExited: false })
        {
            try
            {
                _firecrackerProcess.Kill(entireProcessTree: true);
            }
            catch
            {
                // best effort teardown
            }

            await _firecrackerProcess.WaitForExitAsync().ConfigureAwait(false);
        }

        _firecrackerProcess?.Dispose();
        if (_virtioFsProcess is { HasExited: false })
        {
            try
            {
                _virtioFsProcess.Kill(entireProcessTree: true);
            }
            catch
            {
                // best effort
            }

            await _virtioFsProcess.WaitForExitAsync().ConfigureAwait(false);
        }

        _virtioFsProcess?.Dispose();

        if (!string.IsNullOrWhiteSpace(_tapName))
        {
            await RunShellAsync($"ip link delete {_tapName}", CancellationToken.None).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(ApiSocketPath))
        {
            var workDir = Path.GetDirectoryName(ApiSocketPath);
            if (!string.IsNullOrWhiteSpace(workDir) && Directory.Exists(workDir))
            {
                try
                {
                    Directory.Delete(workDir, recursive: true);
                }
                catch
                {
                    // best effort cleanup
                }
            }
        }
    }

    private async Task WaitForGuestAgentAsync(CancellationToken cancellationToken)
    {
        var client = new VsockGuestClient(VsockUdsPath);
        for (var attempt = 0; attempt < 30; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var ping = await client
                    .ExecuteAsync(
                        new VsockGuestExecuteRequest { User = "root", Script = "true" },
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (ping.ExitCode == 0)
                {
                    return;
                }
            }
            catch
            {
                // guest agent may not be ready yet
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Guest agent did not become ready over vsock.");
    }

    private async Task StartVirtioFsAsync(
        string hostPath,
        string socketPath,
        CancellationToken cancellationToken
    )
    {
        var info = new ProcessStartInfo("virtiofsd")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("--socket-path");
        info.ArgumentList.Add(socketPath);
        info.ArgumentList.Add("--shared-dir");
        info.ArgumentList.Add(hostPath);
        info.ArgumentList.Add("--cache");
        info.ArgumentList.Add("auto");
        var process = Process.Start(info)
            ?? throw new InvalidOperationException("Failed to start virtiofsd.");
        _virtioFsProcess = process;
        await WaitForSocketAsync(socketPath, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WaitForSocketAsync(string socketPath, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (File.Exists(socketPath))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for socket {socketPath}.");
    }

    private static async Task<string> CreateTapAsync(CancellationToken cancellationToken)
    {
        var tapName = $"tap{Guid.NewGuid():N}"[..12];
        await RunShellAsync($"ip tuntap add dev {tapName} mode tap", cancellationToken).ConfigureAwait(false);
        await RunShellAsync($"ip link set dev {tapName} up", cancellationToken).ConfigureAwait(false);
        await RunShellAsync($"ip addr add 172.16.0.1/30 dev {tapName}", cancellationToken).ConfigureAwait(false);
        return tapName;
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
        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Command failed ({process.ExitCode}): {command}\n{stderr}");
        }
    }

    private static string GenerateGuestMac()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        bytes[0] = 0x02;
        return string.Join(":", bytes.Take(6).Select(b => b.ToString("X2")));
    }
}
