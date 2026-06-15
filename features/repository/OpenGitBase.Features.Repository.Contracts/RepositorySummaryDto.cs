namespace OpenGitBase.Features.Repository.Contracts;

public class RepositorySummaryDto
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public long StorageBytesUsed { get; set; }
}
