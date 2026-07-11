using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.Features.Pipeline.Contracts;

public class UpdatePipelineQuery
    : UpdateCommand<PipelineDto, PipelineId, Guid, UpdatePipelineQuery>;
