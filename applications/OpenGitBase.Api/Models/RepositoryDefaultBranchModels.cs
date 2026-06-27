#pragma warning disable SA1402 // File may only contain a single type
namespace OpenGitBase.Api.Models;

public sealed class UpdateRepositoryDefaultBranchRequest
{
    public string DefaultBranchName { get; set; } = string.Empty;
}

public sealed class RepositoryDefaultBranchResponse
{
    public string? DefaultBranchName { get; set; }
}
