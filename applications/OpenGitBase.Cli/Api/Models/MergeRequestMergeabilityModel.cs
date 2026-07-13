namespace OpenGitBase.Cli.Api.Models;

public sealed class MergeRequestMergeabilityModel
{
    public string Status { get; set; } = string.Empty;

    public string? Message { get; set; }
}
