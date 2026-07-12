using System.Diagnostics;

namespace OpenGitBase.ComputeAgent;

public sealed class OverlayFsStackAssembler : IOverlayFsStackAssembler
{
    private readonly HashSet<string> _activeMounts = new(StringComparer.Ordinal);

    public async Task<OverlayFsStackAssemblyResult> AssembleAsync(
        OverlayFsStackRequest request,
        CancellationToken cancellationToken
    )
    {
        var logs = new List<string>();
        if (string.IsNullOrWhiteSpace(request.BaseImageArtifactPath))
        {
            return Failure("Base image artifact path is required.", logs);
        }

        var stackRoot = Path.Combine(
            request.WorkRoot,
            "overlay",
            request.JobId.ToString("N")
        );
        Directory.CreateDirectory(stackRoot);
        var lowerRoot = Path.Combine(stackRoot, "lower");
        var upperRoot = Path.Combine(stackRoot, "upper");
        var workRoot = Path.Combine(stackRoot, "work");
        var mergedRoot = Path.Combine(stackRoot, "merged");
        Directory.CreateDirectory(upperRoot);
        Directory.CreateDirectory(workRoot);
        Directory.CreateDirectory(mergedRoot);

        try
        {
            await ExtractArtifactAsync(request.BaseImageArtifactPath, lowerRoot, cancellationToken)
                .ConfigureAwait(false);
            logs.Add($"Mounted base image layer from {request.BaseImageArtifactPath}.");

            var lowerDirs = new List<string> { lowerRoot };
            foreach (var dependencyLayer in request.DependencyLayerPaths)
            {
                if (string.IsNullOrWhiteSpace(dependencyLayer))
                {
                    continue;
                }

                var layerDir = Path.Combine(stackRoot, $"dep-{lowerDirs.Count}");
                await ExtractArtifactAsync(dependencyLayer, layerDir, cancellationToken)
                    .ConfigureAwait(false);
                lowerDirs.Add(layerDir);
                logs.Add($"Mounted dependency layer from {dependencyLayer}.");
            }

            if (OperatingSystem.IsLinux() && File.Exists("/proc/filesystems"))
            {
                var overlayAvailable = await File.ReadAllTextAsync("/proc/filesystems", cancellationToken)
                    .ConfigureAwait(false);
                if (overlayAvailable.Contains("overlay", StringComparison.Ordinal))
                {
                    var lowerSpec = string.Join(":", lowerDirs.AsEnumerable().Reverse());
                    var mountArgs =
                        $"-t overlay overlay -o lowerdir={lowerSpec},upperdir={upperRoot},workdir={workRoot} {mergedRoot}";
                    var mountExit = await RunCommandAsync("mount", mountArgs, cancellationToken)
                        .ConfigureAwait(false);
                    if (mountExit == 0)
                    {
                        _activeMounts.Add(mergedRoot);
                        logs.Add("OverlayFS stack mounted for guest rootfs.");
                        return Success(mergedRoot, logs);
                    }

                    logs.Add("OverlayFS mount failed; falling back to merged directory copy.");
                }
            }

            await CopyDirectoryAsync(lowerRoot, mergedRoot, cancellationToken).ConfigureAwait(false);
            foreach (var dependencyLayerDir in lowerDirs.Skip(1))
            {
                await CopyDirectoryAsync(dependencyLayerDir, mergedRoot, cancellationToken)
                    .ConfigureAwait(false);
            }

            logs.Add("Ephemeral upper layer prepared (copy fallback mode).");
            return Success(mergedRoot, logs);
        }
        catch (Exception ex)
        {
            return Failure(ex.Message, logs);
        }
    }

    public async Task TeardownAsync(string mergedRootPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mergedRootPath))
        {
            return;
        }

        if (_activeMounts.Contains(mergedRootPath))
        {
            _ = await RunCommandAsync("umount", mergedRootPath, cancellationToken).ConfigureAwait(false);
            _activeMounts.Remove(mergedRootPath);
        }

        var stackRoot = Directory.GetParent(mergedRootPath)?.FullName;
        if (stackRoot is not null && Directory.Exists(stackRoot))
        {
            Directory.Delete(stackRoot, recursive: true);
        }
    }

    private static OverlayFsStackAssemblyResult Success(string mergedRoot, IReadOnlyList<string> logs) =>
        new()
        {
            Success = true,
            MergedRootPath = mergedRoot,
            LogLines = logs,
        };

    private static OverlayFsStackAssemblyResult Failure(string message, IReadOnlyList<string> logs) =>
        new()
        {
            Success = false,
            ErrorMessage = message,
            LogLines = logs,
        };

    private static async Task ExtractArtifactAsync(
        string artifactPath,
        string destination,
        CancellationToken cancellationToken
    )
    {
        Directory.CreateDirectory(destination);
        if (Directory.Exists(artifactPath) && !File.Exists(artifactPath))
        {
            await CopyDirectoryAsync(artifactPath, destination, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!File.Exists(artifactPath))
        {
            throw new InvalidOperationException($"Artifact not found at {artifactPath}.");
        }

        var exitCode = await RunCommandAsync(
            "tar",
            $"-xzf \"{artifactPath}\" -C \"{destination}\"",
            cancellationToken
        ).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to extract artifact {artifactPath}.");
        }
    }

    private static async Task CopyDirectoryAsync(
        string source,
        string destination,
        CancellationToken cancellationToken
    )
    {
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Path.Combine(destination, Path.GetRelativePath(source, directory));
            Directory.CreateDirectory(target);
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static async Task<int> RunCommandAsync(
        string command,
        string args,
        CancellationToken cancellationToken
    )
    {
        var info = new ProcessStartInfo("sh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add($"{command} {args}");
        using var process = Process.Start(info);
        if (process is null)
        {
            return -1;
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
    }
}
