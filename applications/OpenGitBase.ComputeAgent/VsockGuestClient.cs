using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace OpenGitBase.ComputeAgent;

#pragma warning disable SA1402

public sealed class VsockGuestExecuteRequest
{
    public string User { get; init; } = "ogb";

    public string Cwd { get; init; } = "/workspace/repo";

    public string Script { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Environment { get; init; }
        = new Dictionary<string, string>();
}

public sealed class VsockGuestExecuteResult
{
    public bool Success { get; init; }

    public int ExitCode { get; init; }

    public string StdOut { get; init; } = string.Empty;

    public string StdErr { get; init; } = string.Empty;
}

public sealed class VsockGuestClient
{
    private readonly int _guestCid;
    private readonly int _port;

    public VsockGuestClient(int guestCid, int port)
    {
        _guestCid = guestCid;
        _port = port;
    }

    public VsockGuestClient(string udsPath)
        : this(0, 0)
    {
        UdsPath = udsPath;
    }

    public string? UdsPath { get; }

    public async Task<VsockGuestExecuteResult> ExecuteAsync(
        VsockGuestExecuteRequest request,
        CancellationToken cancellationToken,
        Action<string>? onOutputLine = null
    )
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                user = request.User,
                cwd = request.Cwd,
                script = request.Script,
                env = request.Environment,
            }
        );
        var start = Stopwatch.GetTimestamp();
        var connectTarget = UdsPath is not null
            ? $"UNIX-CONNECT:{UdsPath}"
            : $"VSOCK-CONNECT:{_guestCid}:{_port}";

        var info = new ProcessStartInfo("socat")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("-");
        info.ArgumentList.Add(connectTarget);

        using var process = Process.Start(info);
        if (process is null)
        {
            return new VsockGuestExecuteResult
            {
                Success = false,
                ExitCode = -1,
                StdErr = "Unable to start socat for vsock connection.",
            };
        }

        await process.StandardInput.WriteLineAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
        process.StandardInput.Close();

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stdoutTask = ReadLinesAsync(process.StandardOutput, stdout, onOutputLine, cancellationToken);
        var stderrTask = ReadLinesAsync(process.StandardError, stderr, onOutputLine, cancellationToken);
        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var exitCode = ParseExitCode(stdout.ToString());
        if (exitCode is null && process.ExitCode != 0)
        {
            exitCode = process.ExitCode;
        }

        _ = Stopwatch.GetElapsedTime(start);
        return new VsockGuestExecuteResult
        {
            Success = exitCode == 0,
            ExitCode = exitCode ?? -1,
            StdOut = stdout.ToString(),
            StdErr = stderr.ToString(),
        };
    }

    private static async Task ReadLinesAsync(
        StreamReader reader,
        StringBuilder buffer,
        Action<string>? onOutputLine,
        CancellationToken cancellationToken
    )
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            buffer.AppendLine(line);
            onOutputLine?.Invoke(line);
        }
    }

    private static int? ParseExitCode(string stdout)
    {
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith('{'))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.TryGetProperty("exitCode", out var exitCodeElement)
                    && exitCodeElement.TryGetInt32(out var exitCode))
                {
                    return exitCode;
                }
            }
            catch (JsonException)
            {
                // ignore malformed trailer lines
            }
        }

        return null;
    }
}
