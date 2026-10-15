using System.Diagnostics;
using System.Text.Json;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.GitHttps;

/// <summary>What LFS tests look at on the stack's storage nodes: replica roles, files on disk, <c>docker exec</c>.</summary>
public static class LfsStorageDisk
{
    public const string EncryptedLfsRoot = "/var/lib/opengitbase/lfs";
    public const string PlaintextLfsRoot = "/srv/git/.lfs";

    /// <summary><c>{repository}/aa/bb/{oid}</c>, the place of an object below either LFS root.</summary>
    public static string RelativePath(string repositoryId, string oid) =>
        $"{repositoryId.ToLowerInvariant()}/{oid[..2]}/{oid[2..4]}/{oid}";

    public static async Task<IReadOnlyList<(string NodeId, string Role)>> GetReplicasAsync(
        AuthenticatedClient admin,
        string repositoryId
    )
    {
        using var response = await admin.Client.RawClient
            .GetAsync($"admin/repositories/{repositoryId}/replication")
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.True(response.IsSuccessStatusCode, $"GET replication status: {(int)response.StatusCode} {body}");
        using var document = JsonDocument.Parse(body);
        return document
            .RootElement.GetProperty("replicas")
            .EnumerateArray()
            .Select(replica => (
                replica.GetProperty("nodeId").GetString() ?? string.Empty,
                replica.GetProperty("role").GetString() ?? string.Empty
            ))
            .ToList();
    }

    /// <summary>Polls without the transcript until the file exists and is not empty.</summary>
    public static async Task WaitForFileAsync(string nodeId, string path, TimeSpan deadline)
    {
        var until = DateTimeOffset.UtcNow + deadline;
        while (true)
        {
            var test = await ExecAsync(nodeId, "test", "-s", path).ConfigureAwait(false);
            if (test.ExitCode == 0)
            {
                return;
            }

            Assert.True(DateTimeOffset.UtcNow < until, $"{path} did not appear on {nodeId} within {deadline.TotalSeconds:0} s.");
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
    }

    /// <summary><c>docker exec</c> against the stack's container for a compose service.</summary>
    public static async Task<(int ExitCode, string StdOut, string StdErr)> ExecAsync(string serviceName, params string[] command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(E2eEnvironment.ContainerName(serviceName));
        foreach (var argument in command)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start docker.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        return (process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }

    /// <summary>Waits until the container's health check reports healthy again, e.g. after a restart.</summary>
    public static async Task WaitForHealthyAsync(string serviceName, TimeSpan deadline)
    {
        var until = DateTimeOffset.UtcNow + deadline;
        while (true)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in new[] { "inspect", "-f", "{{.State.Health.Status}}", E2eEnvironment.ContainerName(serviceName) })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start docker.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().ConfigureAwait(false);
            await stderr.ConfigureAwait(false);
            if ((await stdout.ConfigureAwait(false)).Trim() == "healthy")
            {
                return;
            }

            Assert.True(DateTimeOffset.UtcNow < until, $"{serviceName} was not healthy within {deadline.TotalSeconds:0} s.");
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
    }
}
