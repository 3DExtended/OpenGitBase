namespace OpenGitBase.Cli.Tests;

public sealed class CliAppTests
{
    [Fact]
    public async Task Help_prints_usage()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliApp.RunAsync(["--help"], output, error).ConfigureAwait(false);

        Assert.Equal(0, exitCode);
        Assert.Contains("OpenGitBase command-line tool", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Version_prints_semver()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliApp.RunAsync(["--version"], output, error).ConfigureAwait(false);

        Assert.Equal(0, exitCode);
        Assert.Matches(@"\d+\.\d+\.\d+", output.ToString().Trim());
    }

    [Fact]
    public void GetVersion_returns_non_empty()
    {
        Assert.Matches(@"\d+\.\d+\.\d+", CliApp.GetVersion());
    }
}
