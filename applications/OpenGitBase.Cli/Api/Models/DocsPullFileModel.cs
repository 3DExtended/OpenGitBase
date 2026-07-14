namespace OpenGitBase.Cli.Api.Models;

public sealed class DocsPullFileModel
{
    public int Number { get; set; }

    public string Path { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
}
