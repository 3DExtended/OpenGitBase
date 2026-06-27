namespace OpenGitBase.Features.MergeRequest.Contracts;

public class MergeRequestMergeabilityDto
{
    public MergeRequestMergeabilityStatus Status { get; set; }

    public string? Message { get; set; }
}
