using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public class DeletePipelineQueryHandler
    : DeleteCommandHandlerBase<
        DeletePipelineQuery,
        PipelineDto,
        PipelineId,
        Guid,
        OpenGitBaseDbContext,
        Entities.PipelineEntity
    >
{
    public DeletePipelineQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
        : base(contextFactory)
    {
    }
}
