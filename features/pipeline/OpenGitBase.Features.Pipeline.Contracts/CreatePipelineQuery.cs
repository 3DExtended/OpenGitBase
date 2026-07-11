using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.Features.Pipeline.Contracts;

public class CreatePipelineQuery
    : CreateQuery<PipelineDto, PipelineId, Guid, CreatePipelineQuery>;
