using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public class ListPipelineQueryHandler
    : ListOfModelQueryHandlerBase<
        ListPipelineQuery,
        PipelineDto,
        PipelineId,
        Guid,
        OpenGitBaseDbContext,
        Entities.PipelineEntity
    >
{
    public ListPipelineQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(mapper, contextFactory)
    {
    }
}
