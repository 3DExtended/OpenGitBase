namespace OpenGitBase.Api.Controllers;

public sealed class UpdateComputeNodeCapacityRequest
{
    public int MaxConcurrentJobs { get; set; }

    public int MaxCpu { get; set; }

    public long MaxMemoryBytes { get; set; }
}
