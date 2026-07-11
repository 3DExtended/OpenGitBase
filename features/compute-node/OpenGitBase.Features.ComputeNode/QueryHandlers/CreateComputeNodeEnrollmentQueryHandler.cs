using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.Entities;

namespace OpenGitBase.Features.ComputeNode.QueryHandlers;

public sealed class CreateComputeNodeEnrollmentQueryHandler
    : IQueryHandler<CreateComputeNodeEnrollmentQuery, string>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;

    public CreateComputeNodeEnrollmentQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
    }

    public async Task<Option<string>> RunQueryAsync(
        CreateComputeNodeEnrollmentQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            query.MaxConcurrentJobs <= 0
            || query.MaxCpu <= 0
            || query.MaxMemoryBytes <= 0
            || string.IsNullOrWhiteSpace(query.NodeId)
        )
        {
            return Option<string>.None;
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Set<ComputeNodeEnrollmentEntity>()
            .Add(
                new ComputeNodeEnrollmentEntity
                {
                    Id = Guid.NewGuid(),
                    NodeId = query.NodeId,
                    EnrollmentTokenHash = _passwordHasherService.HashPassword(token),
                    CreatedByUserId = query.CreatedByUserId,
                    OrganizationId = query.OrganizationId,
                    HostingScope = query.HostingScope,
                    MaxConcurrentJobs = query.MaxConcurrentJobs,
                    MaxCpu = query.MaxCpu,
                    MaxMemoryBytes = query.MaxMemoryBytes,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(6),
                }
            );
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(token);
    }
}
