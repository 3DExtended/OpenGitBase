using System.CommandLine;
using System.Reflection;

namespace OpenGitBase.Cli;

public static class CliApp
{
    public static RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("OpenGitBase command-line tool")
        {
            new VersionOption(),
        };

        rootCommand.SetAction(_ => 0);

        return rootCommand;
    }

    public static async Task<int> RunAsync(string[] args, TextWriter? output = null, TextWriter? error = null)
    {
        var parseResult = BuildRootCommand().Parse(args);
        var configuration = new InvocationConfiguration
        {
            Output = output ?? Console.Out,
            Error = error ?? Console.Error,
        };

        return await parseResult.InvokeAsync(configuration).ConfigureAwait(false);
    }

    public static string GetVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
}
