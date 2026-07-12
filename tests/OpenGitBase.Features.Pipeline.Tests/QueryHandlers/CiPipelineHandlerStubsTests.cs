using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.Entities;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;
using OpenGitBase.Features.Pipeline.Services;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public class IngestGitPushQueryHandlerTests;

public class SchedulePipelineRunFromPushQueryHandlerTests;

public class ListPipelineRunsQueryHandlerTests;

public class GetPipelineRunQueryHandlerTests;

public class GetPipelineJobQueryHandlerTests;

public class GetPipelineJobLogsQueryHandlerTests;

public class ClaimPipelineJobQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_OnlyClaimsEligibleNodes()
    {
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        var repositoryId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        context.Set<RepositoryEntity>()
            .Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "repo",
                    OwnerUserId = Guid.NewGuid(),
                    Slug = "repo",
                    PhysicalPath = "/tmp/repo.git",
                }
            );
        context.Set<PipelineRunEntity>()
            .Add(
                new PipelineRunEntity
                {
                    Id = runId,
                    RepositoryId = repositoryId,
                    Ref = "main",
                    AfterSha = "abc123",
                    StageOrderJson = "[\"build\"]",
                    Status = PipelineRunStatus.Queued,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        var jobId = Guid.NewGuid();
        context.Set<PipelineJobEntity>()
            .Add(
                new PipelineJobEntity
                {
                    Id = jobId,
                    RunId = runId,
                    Name = "build",
                    Stage = "build",
                    RunsOn = "ogb-hosted",
                    Status = PipelineJobStatus.Queued,
                    Script = "echo ok",
                    ResolvedSpecJson = "{}",
                    EnvironmentJson = "{}",
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        var orgNodeId = Guid.NewGuid();
        context.Set<ComputeNodeEntity>()
            .Add(
                new ComputeNodeEntity
                {
                    Id = orgNodeId,
                    NodeId = "org-node",
                    OrganizationId = Guid.NewGuid(),
                    HostingScope = ComputeHostingScope.OwnOrgOnly,
                    MaxConcurrentJobs = 2,
                    MaxCpu = 2,
                    MaxMemoryBytes = 8L * 1024 * 1024 * 1024,
                    IsHealthy = true,
                    RegisteredAt = DateTimeOffset.UtcNow,
                }
            );
        await context.SaveChangesAsync();

        var handler = new ClaimPipelineJobQueryHandler(
            scope.ContextFactory,
            scope.Mapper,
            new PasswordHasherService()
        );
        var orgClaim = await handler.RunQueryAsync(
            new ClaimPipelineJobQuery
            {
                ComputeNodeId = orgNodeId,
                HostingProfiles = ["ogb-hosted"],
            },
            CancellationToken.None
        );
        Assert.True(orgClaim.IsNone);

        var platformNodeId = Guid.NewGuid();
        context.Set<ComputeNodeEntity>()
            .Add(
                new ComputeNodeEntity
                {
                    Id = platformNodeId,
                    NodeId = "platform-node",
                    OrganizationId = null,
                    HostingScope = ComputeHostingScope.CrossOrgAllowed,
                    MaxConcurrentJobs = 2,
                    MaxCpu = 2,
                    MaxMemoryBytes = 8L * 1024 * 1024 * 1024,
                    IsHealthy = true,
                    RegisteredAt = DateTimeOffset.UtcNow,
                }
            );
        await context.SaveChangesAsync();

        var platformClaim = await handler.RunQueryAsync(
            new ClaimPipelineJobQuery
            {
                ComputeNodeId = platformNodeId,
                HostingProfiles = ["ogb-hosted"],
            },
            CancellationToken.None
        );
        Assert.True(platformClaim.IsSome);
        Assert.Equal(PipelineJobStatus.Running, platformClaim.Get().Job.Status);
    }
}

public class UpdatePipelineJobStatusQueryHandlerTests;

public class CancelPipelineJobQueryHandlerTests;

public class CreateBaseImageCatalogEntryQueryHandlerTests;

public class ListBaseImageCatalogEntriesQueryHandlerTests;

public class AdvancePipelineRunQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_QueuesNextStageAfterPriorStagePasses()
    {
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        var runId = Guid.NewGuid();
        context.Set<PipelineRunEntity>()
            .Add(
                new PipelineRunEntity
                {
                    Id = runId,
                    RepositoryId = Guid.NewGuid(),
                    Ref = "main",
                    AfterSha = "abc",
                    StageOrderJson = "[\"build\",\"test\"]",
                    Status = PipelineRunStatus.Running,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        context.Set<PipelineJobEntity>()
            .AddRange(
                new PipelineJobEntity
                {
                    Id = Guid.NewGuid(),
                    RunId = runId,
                    Name = "build",
                    Stage = "build",
                    RunsOn = "ogb-hosted",
                    Status = PipelineJobStatus.Passed,
                    Script = "echo build",
                    ResolvedSpecJson = "{}",
                    EnvironmentJson = "{}",
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                new PipelineJobEntity
                {
                    Id = Guid.NewGuid(),
                    RunId = runId,
                    Name = "test",
                    Stage = "test",
                    RunsOn = "ogb-hosted",
                    Status = PipelineJobStatus.Blocked,
                    Script = "echo test",
                    ResolvedSpecJson = "{}",
                    EnvironmentJson = "{}",
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        await context.SaveChangesAsync();

        var handler = new AdvancePipelineRunQueryHandler(
            scope.ContextFactory,
            scope.Mapper,
            scope.JobPublisher
        );
        var result = await handler.RunQueryAsync(
            new AdvancePipelineRunQuery { RunId = PipelineRunId.From(runId) },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        await using var verificationContext = await scope.ContextFactory.CreateDbContextAsync();
        var refreshed = await verificationContext
            .Set<PipelineJobEntity>()
            .Where(entity => entity.RunId == runId && entity.Stage == "test")
            .ToListAsync();
        Assert.All(refreshed, job => Assert.Equal(PipelineJobStatus.Queued, job.Status));
    }
}

public class RecordDependencyInstallOutcomeQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_PersistsOutcome()
    {
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var handler = new RecordDependencyInstallOutcomeQueryHandler(scope.ContextFactory);
        var result = await handler.RunQueryAsync(
            new RecordDependencyInstallOutcomeQuery
            {
                JobId = PipelineJobId.From(Guid.NewGuid()),
                RecipeKey = "linux:apt:git",
                Success = true,
                ExitCode = 0,
                DurationMs = 1200,
            },
            CancellationToken.None
        );
        Assert.True(result.IsSome);
    }
}

public class RequestDependencyLayerPromotionQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_RequiresFiveSuccessfulOutcomes()
    {
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        var recipeKey = "linux:apt:docker";
        foreach (var index in Enumerable.Range(0, 5))
        {
            context.Set<DependencyInstallOutcomeEntity>()
                .Add(
                    new DependencyInstallOutcomeEntity
                    {
                        Id = Guid.NewGuid(),
                        RecipeKey = recipeKey,
                        Success = true,
                        ExitCode = 0,
                        DurationMs = 50,
                        CreatedAt = DateTimeOffset.UtcNow,
                    }
                );
        }

        await context.SaveChangesAsync();
        var handler = new RequestDependencyLayerPromotionQueryHandler(scope.ContextFactory, scope.Mapper);
        var result = await handler.RunQueryAsync(
            new RequestDependencyLayerPromotionQuery
            {
                RecipeKey = recipeKey,
                RequestedByUserId = Guid.NewGuid(),
            },
            CancellationToken.None
        );
        Assert.True(result.IsSome);
        Assert.True(result.Get().PromotionJobScheduled);
    }
}

public class SubmitDomainAllowanceRequestQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_CreatesPendingRequest()
    {
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var handler = new SubmitDomainAllowanceRequestQueryHandler(scope.ContextFactory, scope.Mapper);
        var result = await handler.RunQueryAsync(
            new SubmitDomainAllowanceRequestQuery
            {
                Domain = "registry.npmjs.org",
                Justification = "need packages",
                Scope = DomainAllowanceRequestScope.Platform,
                RequestedByUserId = Guid.NewGuid(),
            },
            CancellationToken.None
        );
        Assert.True(result.IsSome);
        Assert.Equal(DomainAllowanceRequestStatus.Pending, result.Get().Status);
    }
}

public class ReviewDomainAllowanceRequestQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ApprovesRequestAndWritesAllowlist()
    {
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        var requestId = Guid.NewGuid();
        context.Set<DomainAllowanceRequestEntity>()
            .Add(
                new DomainAllowanceRequestEntity
                {
                    Id = requestId,
                    Domain = "pypi.org",
                    Justification = "python deps",
                    Scope = DomainAllowanceRequestScope.Platform,
                    Status = DomainAllowanceRequestStatus.Pending,
                    RequestedByUserId = Guid.NewGuid(),
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        await context.SaveChangesAsync();

        var handler = new ReviewDomainAllowanceRequestQueryHandler(scope.ContextFactory, scope.Mapper);
        var result = await handler.RunQueryAsync(
            new ReviewDomainAllowanceRequestQuery
            {
                RequestId = DomainAllowanceRequestId.From(requestId),
                Approve = true,
                ReviewedByUserId = Guid.NewGuid(),
            },
            CancellationToken.None
        );
        Assert.True(result.IsSome);
        Assert.Equal(DomainAllowanceRequestStatus.Approved, result.Get().Status);
    }
}

public class ResolveEffectiveEgressAllowlistQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_MergesPlatformAndOrgForOrgHosted()
    {
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        var orgId = Guid.NewGuid();
        context.Set<PlatformEgressAllowlistEntity>()
            .Add(
                new PlatformEgressAllowlistEntity
                {
                    Id = Guid.NewGuid(),
                    Domain = "registry.npmjs.org",
                    ApprovedByUserId = Guid.NewGuid(),
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        context.Set<OrgEgressAllowlistEntity>()
            .Add(
                new OrgEgressAllowlistEntity
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    Domain = "internal.artifacts.example",
                    ApprovedByUserId = Guid.NewGuid(),
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        await context.SaveChangesAsync();

        var handler = new ResolveEffectiveEgressAllowlistQueryHandler(scope.ContextFactory);
        var result = await handler.RunQueryAsync(
            new ResolveEffectiveEgressAllowlistQuery
            {
                RunsOn = "organization-self-hosted",
                OrganizationId = orgId,
            },
            CancellationToken.None
        );
        Assert.True(result.IsSome);
        Assert.Contains("registry.npmjs.org", result.Get());
        Assert.Contains("internal.artifacts.example", result.Get());
    }
}

public class ListDomainAllowanceRequestsQueryHandlerTests;

public class ListDependencyInstallAnalyticsQueryHandlerTests;

public class ListDependencyPromotionRequestsQueryHandlerTests;

public class ValidateJobIdentityQueryHandlerTests;

public class ResolvePromotedDependencyLayerQueryHandlerTests;

internal sealed class PipelineHandlerTestScope : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    private PipelineHandlerTestScope(SqliteConnection connection, ServiceProvider provider)
    {
        _connection = connection;
        _provider = provider;
    }

    public IDbContextFactory<OpenGitBaseDbContext> ContextFactory =>
        _provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();

    public IMapper Mapper => _provider.GetRequiredService<IMapper>();

    public IJobAvailableEventPublisher JobPublisher =>
        _provider.GetRequiredService<IJobAvailableEventPublisher>();

    public static async Task<PipelineHandlerTestScope> CreateAsync()
    {
        var connection = SqliteTestConnection.OpenInMemory();
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider(
                [
                    typeof(PipelineMapsterConfig).Assembly,
                    typeof(ComputeNodeEntity).Assembly,
                    typeof(RepositoryEntity).Assembly,
                ]
            )
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddSingleton<IJobAvailableEventPublisher>(
            Substitute.For<IJobAvailableEventPublisher>()
        );
        var config = new TypeAdapterConfig();
        new PipelineMapsterConfig().Register(config);
        services.AddSingleton(config);
        services.AddSingleton<IMapper>(new Mapper(config));
        var provider = services.BuildServiceProvider();
        await using var context = await provider
            .GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>()
            .CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
        return new PipelineHandlerTestScope(connection, provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
