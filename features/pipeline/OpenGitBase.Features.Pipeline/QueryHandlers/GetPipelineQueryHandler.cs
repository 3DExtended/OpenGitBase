using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public class GetPipelineQueryHandler
    : SingleModelQueryHandlerBase<
        GetPipelineQuery,
        PipelineDto,
        PipelineId,
        Guid,
        OpenGitBaseDbContext,
        Entities.PipelineEntity
    >
{
    public GetPipelineQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(mapper, contextFactory)
    {
    }
}
