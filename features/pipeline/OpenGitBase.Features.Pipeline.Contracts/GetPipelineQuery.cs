using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.Features.Pipeline.Contracts;

public class GetPipelineQuery
    : SingleModelQuery<PipelineDto, PipelineId, Guid, GetPipelineQuery>;
