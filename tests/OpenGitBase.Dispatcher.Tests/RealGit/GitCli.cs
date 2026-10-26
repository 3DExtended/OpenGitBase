using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OpenGitBase.Dispatcher.Tests.RealGit;

/// <summary>Runs the git client with no system, global or credential configuration of the machine.</summary>
internal sealed class GitCli
{
    private readonly string _home;
    private readonly string _authorizationHeader;

    public GitCli(string home, string accessToken)
    {
        _home = home;
        _authorizationHeader = "Authorization: Basic "
            + Convert.ToBase64String(Encoding.UTF8.GetBytes("git:" + accessToken));
    }

    public async Task<string> RunCheckedAsync(string workingDirectory, params string[] args)
    {
        var result = await RunAsync(workingDirectory, args);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', args)} exited {result.ExitCode}: {result.StdErr}"
            );
        }

        return result.StdOut.Trim();
    }

    public async Task<GitResult> RunAsync(string workingDirectory, params string[] args)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        IsolateGitEnvironment(startInfo.Environment, _home);
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_AUTHOR_NAME"] = "Real Git Test";
        startInfo.Environment["GIT_AUTHOR_EMAIL"] = "real-git@opengitbase.test";
        startInfo.Environment["GIT_COMMITTER_NAME"] = "Real Git Test";
        startInfo.Environment["GIT_COMMITTER_EMAIL"] = "real-git@opengitbase.test";
        var config = new[]
        {
            ("init.defaultBranch", "main"),
            ("credential.helper", string.Empty),
            ("commit.gpgSign", "false"),
            ("tag.gpgSign", "false"),
            ("http.extraHeader", _authorizationHeader),
        };
        startInfo.Environment["GIT_CONFIG_COUNT"] = config.Length.ToString(CultureInfo.InvariantCulture);
        for (var i = 0; i < config.Length; i++)
        {
            startInfo.Environment[$"GIT_CONFIG_KEY_{i}"] = config[i].Item1;
            startInfo.Environment[$"GIT_CONFIG_VALUE_{i}"] = config[i].Item2;
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("git did not start");
        process.StandardInput.Close();
        var stdOut = process.StandardOutput.ReadToEndAsync();
        var stdErr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"git {string.Join(' ', args)} did not finish within two minutes");
        }

        return new GitResult(process.ExitCode, await stdOut, await stdErr);
    }

    /// <summary>Drops the machine's git configuration and any GIT_* variable the test run inherited.</summary>
    internal static void IsolateGitEnvironment(IDictionary<string, string?> environment, string home)
    {
        foreach (var key in environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.Ordinal)).ToList())
        {
            environment.Remove(key);
        }

        environment["HOME"] = home;
        environment["XDG_CONFIG_HOME"] = Path.Combine(home, ".config");
        environment["GIT_CONFIG_NOSYSTEM"] = "1";
        environment["GIT_CONFIG_GLOBAL"] = Path.Combine(home, ".gitconfig-none");
    }
}
