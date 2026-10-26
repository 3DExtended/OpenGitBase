using DotNet.Testcontainers.Configurations;

namespace OpenGitBase.Api.Tests.Base;

/// <summary>
/// A fact that needs a real database server started through Testcontainers. Skipped where no
/// Docker endpoint is reachable, such as the ogb-hosted CI sandbox.
/// </summary>
public sealed class RequiresDockerFactAttribute : FactAttribute
{
    public RequiresDockerFactAttribute()
    {
        if (TestcontainersSettings.OS.DockerEndpointAuthConfig == null)
        {
            Skip = "Docker is not available; this test starts a Postgres container through Testcontainers.";
        }
    }
}
