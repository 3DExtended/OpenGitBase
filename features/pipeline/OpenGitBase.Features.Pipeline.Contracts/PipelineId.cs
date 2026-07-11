using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Pipeline.Contracts;

public record PipelineId : Identifier<Guid, PipelineId>;
