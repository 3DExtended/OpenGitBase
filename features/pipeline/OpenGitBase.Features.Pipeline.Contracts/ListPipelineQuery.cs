using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.Features.Pipeline.Contracts;

public class ListPipelineQuery
    : ListOfModelQuery<PipelineDto, PipelineId, Guid, ListPipelineQuery>;
