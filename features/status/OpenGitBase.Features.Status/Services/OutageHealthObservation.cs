using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Features.Status.Services;

public sealed class OutageHealthObservation
{
    public OutageWindowScope Scope { get; set; }

    public StatusComponentGroup Group { get; set; }

    public string? InstanceId { get; set; }

    /// <summary>Health of this observation key (group rollup or instance).</summary>
    public PublicHealthStatus Status { get; set; }

    /// <summary>Parent group rollup (for partial detection).</summary>
    public PublicHealthStatus GroupStatus { get; set; }
}
