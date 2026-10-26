using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.Entities;

namespace OpenGitBase.Features.ComputeNode.QueryHandlers;

public sealed class RegisterComputeNodeQueryHandler
    : IQueryHandler<RegisterComputeNodeQuery, RegisterComputeNodeResultDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IMapper _mapper;

    public RegisterComputeNodeQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
        _mapper = mapper;
    }

    public async Task<Option<RegisterComputeNodeResultDto>> RunQueryAsync(
        RegisterComputeNodeQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var enrollments = await context
            .Set<ComputeNodeEnrollmentEntity>()
            .Where(entity => entity.NodeId == query.NodeId && entity.ConsumedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var enrollment = enrollments
            .OrderByDescending(entity => entity.CreatedAt)
            .FirstOrDefault();
        if (
            enrollment is null
            || enrollment.ExpiresAt < DateTimeOffset.UtcNow
            || !_passwordHasherService.VerifyPassword(enrollment.EnrollmentTokenHash, query.EnrollmentToken)
        )
        {
            return Option<RegisterComputeNodeResultDto>.None;
        }

        var consumedAt = DateTimeOffset.UtcNow;
        var node = new ComputeNodeEntity
        {
            Id = Guid.NewGuid(),
            NodeId = query.NodeId,
            OrganizationId = enrollment.OrganizationId,
            HostingScope = enrollment.HostingScope,
            MaxConcurrentJobs = enrollment.MaxConcurrentJobs,
            MaxCpu = enrollment.MaxCpu,
            MaxMemoryBytes = enrollment.MaxMemoryBytes,
            IsHealthy = true,
            RegisteredAt = DateTimeOffset.UtcNow,
            LastHeartbeatAt = DateTimeOffset.UtcNow,
        };
        var identityToken = ComputeNodeIdentityTokens.Mint(node.Id);
        node.IdentityTokenHash = _passwordHasherService.HashPassword(identityToken);
        await context
            .InsertOnceAsync(
                async ct =>
                {
                    // Every attempt starts with an empty change tracker, so the enrollment is loaded
                    // again to be consumed.
                    var pendingEnrollment = await context
                        .Set<ComputeNodeEnrollmentEntity>()
                        .SingleAsync(entity => entity.Id == enrollment.Id, ct)
                        .ConfigureAwait(false);
                    pendingEnrollment.ConsumedAt = consumedAt;
                    context.Set<ComputeNodeEntity>().Add(node);
                    return await context.SaveChangesAsync(ct).ConfigureAwait(false);
                },
                ct => context.Set<ComputeNodeEntity>().AnyAsync(entity => entity.Id == node.Id, ct),
                cancellationToken
            )
            .ConfigureAwait(false);
        return Option.From(
            new RegisterComputeNodeResultDto
            {
                Node = _mapper.Map<ComputeNodeDto>(node),
                NodeIdentityToken = identityToken,
            }
        );
    }
}
