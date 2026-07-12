namespace OpenGitBase.Cli;

public static class Program
{
    public static Task<int> Main(string[] args) => CliApp.RunAsync(args);
}
