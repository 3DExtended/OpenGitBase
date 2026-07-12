using System.Diagnostics;

namespace OpenGitBase.Cli.Auth;

public sealed class SystemBrowserLauncher : IBrowserLauncher
{
    public void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }
}
