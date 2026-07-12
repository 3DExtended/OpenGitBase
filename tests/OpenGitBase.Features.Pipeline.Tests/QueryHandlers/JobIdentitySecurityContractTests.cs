using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public class JobIdentitySecurityContractTests
{
    [Fact]
    public async Task ValidateJobIdentity_RejectsCrossRepositoryAccess()
    {
        var repositoryA = Guid.NewGuid();
        var repositoryB = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var token = JobIdentityTokens.Mint(jobId);
        var hasher = new PasswordHasherService();

        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PipelineMapsterConfig>(
            typeof(ValidateJobIdentityQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();
        await using (var seed = await scope.CreateDbContextAsync())
        {
            seed.Set<PipelineRunEntity>().Add(
                new PipelineRunEntity
                {
                    Id = runId,
                    RepositoryId = repositoryA,
                    AfterSha = "abc123",
                    Ref = "main",
                    Status = PipelineRunStatus.Running,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
            seed.Set<PipelineJobEntity>().Add(
                new PipelineJobEntity
                {
                    Id = jobId,
                    RunId = runId,
                    Name = "test",
                    Stage = "test",
                    RunsOn = "ogb-hosted",
                    Status = PipelineJobStatus.Running,
                    Script = "echo ok",
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
            seed.Set<JobIdentityEntity>().Add(
                new JobIdentityEntity
                {
                    Id = Guid.NewGuid(),
                    JobId = jobId,
                    TokenHash = hasher.HashPassword(token),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                }
            );
            await seed.SaveChangesAsync();
        }

        var handler = new ValidateJobIdentityQueryHandler(
            scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>(),
            hasher
        );
        var result = await handler.RunQueryAsync(
            new ValidateJobIdentityQuery
            {
                Token = token,
                RepositoryId = repositoryB,
                AfterSha = "abc123",
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(result, validation =>
        {
            Assert.False(validation.IsValid);
            Assert.Contains("different repository", validation.Reason, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task ValidateJobIdentity_RejectsRevokedToken()
    {
        var repositoryId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var token = JobIdentityTokens.Mint(jobId);
        var hasher = new PasswordHasherService();

        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PipelineMapsterConfig>(
            typeof(ValidateJobIdentityQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();
        await using (var seed = await scope.CreateDbContextAsync())
        {
            seed.Set<PipelineRunEntity>().Add(
                new PipelineRunEntity
                {
                    Id = runId,
                    RepositoryId = repositoryId,
                    AfterSha = "deadbeef",
                    Ref = "main",
                    Status = PipelineRunStatus.Running,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
            seed.Set<PipelineJobEntity>().Add(
                new PipelineJobEntity
                {
                    Id = jobId,
                    RunId = runId,
                    Name = "test",
                    Stage = "test",
                    RunsOn = "ogb-hosted",
                    Status = PipelineJobStatus.Running,
                    Script = "echo ok",
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
            seed.Set<JobIdentityEntity>().Add(
                new JobIdentityEntity
                {
                    Id = Guid.NewGuid(),
                    JobId = jobId,
                    TokenHash = hasher.HashPassword(token),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                    RevokedAt = DateTimeOffset.UtcNow,
                }
            );
            await seed.SaveChangesAsync();
        }

        var handler = new ValidateJobIdentityQueryHandler(
            scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>(),
            hasher
        );
        var result = await handler.RunQueryAsync(
            new ValidateJobIdentityQuery
            {
                Token = token,
                RepositoryId = repositoryId,
                AfterSha = "deadbeef",
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(result, validation =>
        {
            Assert.False(validation.IsValid);
            Assert.Contains("revoked", validation.Reason, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task ValidateJobIdentity_RejectsExpiredToken()
    {
        var repositoryId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var token = JobIdentityTokens.Mint(jobId);
        var hasher = new PasswordHasherService();

        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PipelineMapsterConfig>(
            typeof(ValidateJobIdentityQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();
        await using (var seed = await scope.CreateDbContextAsync())
        {
            seed.Set<PipelineRunEntity>().Add(
                new PipelineRunEntity
                {
                    Id = runId,
                    RepositoryId = repositoryId,
                    AfterSha = "deadbeef",
                    Ref = "main",
                    Status = PipelineRunStatus.Running,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
            seed.Set<PipelineJobEntity>().Add(
                new PipelineJobEntity
                {
                    Id = jobId,
                    RunId = runId,
                    Name = "test",
                    Stage = "test",
                    RunsOn = "ogb-hosted",
                    Status = PipelineJobStatus.Running,
                    Script = "echo ok",
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
            seed.Set<JobIdentityEntity>().Add(
                new JobIdentityEntity
                {
                    Id = Guid.NewGuid(),
                    JobId = jobId,
                    TokenHash = hasher.HashPassword(token),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                }
            );
            await seed.SaveChangesAsync();
        }

        var handler = new ValidateJobIdentityQueryHandler(
            scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>(),
            hasher
        );
        var result = await handler.RunQueryAsync(
            new ValidateJobIdentityQuery
            {
                Token = token,
                RepositoryId = repositoryId,
                AfterSha = "deadbeef",
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(result, validation =>
        {
            Assert.False(validation.IsValid);
            Assert.Contains("expired", validation.Reason, StringComparison.OrdinalIgnoreCase);
        });
    }
}
