using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class GetPipelineJobQueryHandler : IQueryHandler<GetPipelineJobQuery, PipelineJobDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public GetPipelineJobQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory, IMapper mapper)
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<PipelineJobDto>> RunQueryAsync(
        GetPipelineJobQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var job = await context
            .Set<PipelineJobEntity>()
            .FirstOrDefaultAsync(entity => entity.Id == query.JobId.Value, cancellationToken)
            .ConfigureAwait(false);
        return job is null ? Option<PipelineJobDto>.None : Option.From(_mapper.Map<PipelineJobDto>(job));
    }
}
