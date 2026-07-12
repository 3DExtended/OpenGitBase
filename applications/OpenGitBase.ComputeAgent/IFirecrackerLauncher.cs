namespace OpenGitBase.ComputeAgent;

public interface IFirecrackerLauncher
{
    Task<FirecrackerLaunchResult> LaunchAsync(
        FirecrackerLaunchRequest request,
        CancellationToken cancellationToken
    );
}
