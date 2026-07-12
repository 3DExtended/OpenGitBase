namespace OpenGitBase.Cli.Configuration;

public sealed class OgbConfigFile
{
    public string? ActiveHost { get; set; }

    public string? LoggedInUsername { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}
