using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class MaterializeWorkspaceArchiveQuery
    : IQuery<WorkspaceArchiveResult, MaterializeWorkspaceArchiveQuery>
{
    public PipelineJobId JobId { get; set; } = PipelineJobId.From(Guid.NewGuid());

    public string JobIdentityToken { get; set; } = string.Empty;
}

public sealed class WorkspaceArchiveResult
{
    public byte[] ArchiveBytes { get; set; } = [];

    public string FileName { get; set; } = "workspace.tar.gz";

    public bool IsShallow { get; set; }
}
