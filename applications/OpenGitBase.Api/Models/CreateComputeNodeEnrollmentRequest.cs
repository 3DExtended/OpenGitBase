using OpenGitBase.Features.ComputeNode.Contracts;

namespace OpenGitBase.Api.Models;

public sealed class CreateComputeNodeEnrollmentRequest
{
    public string NodeId { get; init; } = string.Empty;

    public ComputeHostingScope HostingScope { get; init; } = ComputeHostingScope.OwnOrgOnly;

    public int MaxConcurrentJobs { get; init; } = 1;

    public int MaxCpu { get; init; } = 1;

    public long MaxMemoryBytes { get; init; } = 2L * 1024 * 1024 * 1024;
}
