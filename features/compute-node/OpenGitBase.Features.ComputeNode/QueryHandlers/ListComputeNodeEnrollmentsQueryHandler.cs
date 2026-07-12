using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.Entities;

namespace OpenGitBase.Features.ComputeNode.QueryHandlers;

public sealed class ListComputeNodeEnrollmentsQueryHandler
    : IQueryHandler<ListComputeNodeEnrollmentsQuery, IReadOnlyList<ComputeNodeEnrollmentDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListComputeNodeEnrollmentsQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<ComputeNodeEnrollmentDto>>> RunQueryAsync(
        ListComputeNodeEnrollmentsQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var queryable = context.Set<ComputeNodeEnrollmentEntity>().AsNoTracking();
        if (query.OrganizationId is Guid organizationId)
        {
            queryable = queryable.Where(entity => entity.OrganizationId == organizationId);
        }
        else
        {
            queryable = queryable.Where(entity => entity.OrganizationId == null);
        }

        var entities = await queryable.ToListAsync(cancellationToken).ConfigureAwait(false);

        var enrollments = entities
            .OrderByDescending(entity => entity.CreatedAt)
            .Select(entity => new ComputeNodeEnrollmentDto
            {
                Id = entity.Id,
                NodeId = entity.NodeId,
                CreatedAt = entity.CreatedAt,
                ExpiresAt = entity.ExpiresAt,
                ConsumedAt = entity.ConsumedAt,
                OrganizationId = entity.OrganizationId,
                HostingScope = entity.HostingScope,
                MaxConcurrentJobs = entity.MaxConcurrentJobs,
                MaxCpu = entity.MaxCpu,
                MaxMemoryBytes = entity.MaxMemoryBytes,
            })
            .ToList();

        return Option.From<IReadOnlyList<ComputeNodeEnrollmentDto>>(enrollments);
    }
}
