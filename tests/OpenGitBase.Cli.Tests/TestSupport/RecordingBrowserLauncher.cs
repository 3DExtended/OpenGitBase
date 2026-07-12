namespace OpenGitBase.Cli.Tests.TestSupport;

public sealed class RecordingBrowserLauncher : OpenGitBase.Cli.Auth.IBrowserLauncher
{
    public string? LastUrl { get; private set; }

    public void OpenUrl(string url) => LastUrl = url;
}
