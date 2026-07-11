using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed record PipelineRunId : Identifier<Guid, PipelineRunId>;
