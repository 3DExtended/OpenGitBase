using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed record PipelineJobId : Identifier<Guid, PipelineJobId>;
