using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.Entities;

namespace OpenGitBase.Features.ComputeNode.QueryHandlers;

public sealed class UpdateComputeNodeCapacityQueryHandler
    : IQueryHandler<UpdateComputeNodeCapacityQuery, ComputeNodeDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public UpdateComputeNodeCapacityQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<ComputeNodeDto>> RunQueryAsync(
        UpdateComputeNodeCapacityQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var node = await context
            .Set<ComputeNodeEntity>()
            .FirstOrDefaultAsync(entity => entity.Id == query.ComputeNodeId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (node is null)
        {
            return Option<ComputeNodeDto>.None;
        }

        if (query.MaxConcurrentJobs < node.RunningJobs)
        {
            return Option<ComputeNodeDto>.None;
        }

        node.MaxConcurrentJobs = query.MaxConcurrentJobs;
        node.MaxCpu = query.MaxCpu;
        node.MaxMemoryBytes = query.MaxMemoryBytes;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(_mapper.Map<ComputeNodeDto>(node));
    }
}
