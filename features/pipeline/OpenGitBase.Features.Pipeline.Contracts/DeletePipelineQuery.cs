using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.Features.Pipeline.Contracts;

public class DeletePipelineQuery
    : DeleteCommand<PipelineDto, PipelineId, Guid, DeletePipelineQuery>;
