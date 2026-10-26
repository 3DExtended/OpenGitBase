using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.QueryHandlers;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;
using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Api.Tests.Integration;

public class WorkspaceArchiveAuthenticationTests
{
    [Fact]
    public async Task MaterializeWorkspaceArchive_RejectsNodeIdentityToken()
    {
        await using var scope = await OrgComputeTestScope.CreateAsync();
        var enrollment = await scope.CreateEnrollmentHandler.RunQueryAsync(
            new CreateComputeNodeEnrollmentQuery
            {
                NodeId = "ws-node",
                CreatedByUserId = Guid.NewGuid(),
                MaxConcurrentJobs = 1,
                MaxCpu = 1,
                MaxMemoryBytes = 1024,
            },
            CancellationToken.None
        );
        var register = await scope.RegisterHandler.RunQueryAsync(
            new RegisterComputeNodeQuery
            {
                NodeId = "ws-node",
                EnrollmentToken = enrollment.Get().EnrollmentToken,
            },
            CancellationToken.None
        );
        var nodeToken = register.Get().NodeIdentityToken;
        var handler = new MaterializeWorkspaceArchiveQueryHandler(
            scope.ContextFactory,
            new PasswordHasherService(),
            Substitute.For<IRepositoryArchiveReader>()
        );
        var result = await handler.RunQueryAsync(
            new MaterializeWorkspaceArchiveQuery
            {
                JobId = PipelineJobId.From(Guid.NewGuid()),
                JobIdentityToken = nodeToken,
            },
            CancellationToken.None
        );
        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task MaterializeWorkspaceArchive_RejectsExpiredJobIdentity()
    {
        await using var scope = await OrgComputeTestScope.CreateAsync();
        var hasher = new PasswordHasherService();
        var jobId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var token = JobIdentityTokens.Mint(jobId);
        await using (var context = await scope.ContextFactory.CreateDbContextAsync())
        {
            context.Set<PipelineRunEntity>().Add(
                new PipelineRunEntity
                {
                    Id = runId,
                    RepositoryId = Guid.NewGuid(),
                    AfterSha = "abc",
                    Ref = "main",
                    Status = PipelineRunStatus.Running,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
            context.Set<PipelineJobEntity>().Add(
                new PipelineJobEntity
                {
                    Id = jobId,
                    RunId = runId,
                    Name = "build",
                    Stage = "build",
                    RunsOn = "ogb-hosted",
                    Status = PipelineJobStatus.Running,
                    Script = "echo ok",
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
            context.Set<JobIdentityEntity>().Add(
                new JobIdentityEntity
                {
                    Id = Guid.NewGuid(),
                    JobId = jobId,
                    TokenHash = hasher.HashPassword(token),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                }
            );
            await context.SaveChangesAsync();
        }

        var handler = new MaterializeWorkspaceArchiveQueryHandler(
            scope.ContextFactory,
            hasher,
            Substitute.For<IRepositoryArchiveReader>()
        );
        var result = await handler.RunQueryAsync(
            new MaterializeWorkspaceArchiveQuery
            {
                JobId = PipelineJobId.From(jobId),
                JobIdentityToken = token,
            },
            CancellationToken.None
        );
        Assert.True(result.IsNone);
    }

    // The agent fails the job on any error; 503 tells it storage was down, not that its token
    // was rejected.
    [Fact]
    public async Task DownloadWorkspaceArchive_WhenStorageCannotBuildIt_ReturnsServiceUnavailable()
    {
        await using var scope = await OrgComputeTestScope.CreateAsync();
        var jobId = Guid.NewGuid();
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<MaterializeWorkspaceArchiveQuery>(), Arg.Any<CancellationToken>())
            .Returns<Option<WorkspaceArchiveResult>>(_ =>
                throw new RepositoryFileUnavailableException("storage node unreachable")
            );
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = $"Bearer {JobIdentityTokens.Mint(jobId)}";
        var controller = new PipelineController(
            queryProcessor,
            new RepositoryContentAuthorizationService(queryProcessor, new HttpContextAccessor()),
            new ComputeNodeIdentityService(scope.ContextFactory, new PasswordHasherService())
        )
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };

        var result = await controller.DownloadWorkspaceArchive(jobId, CancellationToken.None);

        var status = Assert.IsAssignableFrom<IStatusCodeActionResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
    }
}
