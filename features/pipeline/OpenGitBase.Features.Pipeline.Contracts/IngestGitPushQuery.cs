using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class IngestGitPushQuery : IQuery<bool, IngestGitPushQuery>
{
    public Guid RepositoryId { get; set; }

    public string Ref { get; set; } = string.Empty;

    public string AfterSha { get; set; } = string.Empty;
}
