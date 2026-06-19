namespace OpenGitBase.Common.Options;

public sealed class GitOptions
{
    public string PublicBaseUrl { get; set; } = "https://opengitbase.com";

    public bool SshEnabled { get; set; }
}
