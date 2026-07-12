using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class ValidateJobIdentityQuery : IQuery<JobIdentityValidationResult, ValidateJobIdentityQuery>
{
    public string Token { get; set; } = string.Empty;

    public Guid RepositoryId { get; set; }

    public string AfterSha { get; set; } = string.Empty;
}

public sealed class JobIdentityValidationResult
{
    public bool IsValid { get; init; }

    public string Reason { get; init; } = string.Empty;

    public Guid JobId { get; init; }
}
