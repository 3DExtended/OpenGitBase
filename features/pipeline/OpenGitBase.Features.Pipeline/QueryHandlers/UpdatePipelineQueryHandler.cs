using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public class UpdatePipelineQueryHandler
    : UpdateCommandHandlerBase<
        UpdatePipelineQuery,
        PipelineDto,
        PipelineId,
        Guid,
        OpenGitBaseDbContext,
        Entities.PipelineEntity
    >
{
    public UpdatePipelineQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(mapper, contextFactory)
    {
    }
}
