using System.CommandLine;
using System.Reflection;
using OpenGitBase.Cli.Configuration;

namespace OpenGitBase.Cli;

public static class CliApp
{
    public static RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("OpenGitBase command-line tool")
        {
            new VersionOption(),
            CliOptions.HostnameOption,
            CliOptions.JsonOption,
        };

        rootCommand.SetAction(parseResult =>
        {
            _ = CreateContext(parseResult);
            return 0;
        });

        return rootCommand;
    }

    public static CliRuntimeContext CreateContext(ParseResult parseResult)
    {
        var hostname = parseResult.GetValue(CliOptions.HostnameOption);
        return new CliRuntimeContext(new HostResolver(), new FileConfigStore(), hostname);
    }

    public static Task<int> RunAsync(string[] args, TextWriter? output = null, TextWriter? error = null)
    {
        var parseResult = BuildRootCommand().Parse(args);
        var configuration = new InvocationConfiguration
        {
            Output = output ?? Console.Out,
            Error = error ?? Console.Error,
        };

        return parseResult.InvokeAsync(configuration);
    }

    public static string GetVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
}
