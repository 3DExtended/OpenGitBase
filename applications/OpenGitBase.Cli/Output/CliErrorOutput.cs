namespace OpenGitBase.Cli.Output;

public sealed class CliErrorOutput
{
    public required string Error { get; init; }

    public int? HttpStatus { get; init; }

    public string? Detail { get; init; }
}
