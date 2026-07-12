using System.CommandLine;

namespace OpenGitBase.Cli;

public static class CliOptions
{
    public static Option<string?> HostnameOption { get; } =
        new("--hostname")
        {
            Description = "OpenGitBase host URL (default: https://www.opengitbase.com/)",
        };

    public static Option<bool> JsonOption { get; } =
        new("--json")
        {
            Description = "Output JSON",
        };
}
