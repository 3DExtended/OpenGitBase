using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Api.Tests.Integration;

public class PromotionArtifactUploadTests
{
    [Fact]
    public async Task UploadPromotionArtifact_FromNodeThatClaimedThePromotionJob_CompletesPromotion()
    {
        await using var scope = await OrgComputeTestScope.CreateAsync();
        var claimer = await RegisterNodeAsync(scope, "promotion-runner");
        var promotionId = await SeedRunningPromotionAsync(scope, claimer.Id);
        var layerStore = Substitute.For<ILayerStoreClient>();

        var result = await UploadAsync(scope, claimer.Token, promotionId, layerStore);

        Assert.IsType<AcceptedResult>(result);
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        var promotion = await context.Set<DependencyPromotionRequestEntity>().SingleAsync();
        Assert.Equal(DependencyPromotionRequestStatus.Completed, promotion.Status);
    }

    [Fact]
    public async Task UploadPromotionArtifact_FromAnotherNode_IsRejectedAndLeavesPromotionRunning()
    {
        await using var scope = await OrgComputeTestScope.CreateAsync();
        var claimer = await RegisterNodeAsync(scope, "promotion-runner");
        var otherNode = await RegisterNodeAsync(scope, "org-node");
        var promotionId = await SeedRunningPromotionAsync(scope, claimer.Id);
        var layerStore = Substitute.For<ILayerStoreClient>();

        var result = await UploadAsync(scope, otherNode.Token, promotionId, layerStore);

        Assert.IsType<UnauthorizedResult>(result);
        await layerStore
            .DidNotReceiveWithAnyArgs()
            .PutBlobAsync(default!, default!, default);
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        var promotion = await context.Set<DependencyPromotionRequestEntity>().SingleAsync();
        Assert.Equal(DependencyPromotionRequestStatus.Running, promotion.Status);
        Assert.Null(promotion.ContentHash);
    }

    private static async Task<IActionResult> UploadAsync(
        OrgComputeTestScope scope,
        string nodeToken,
        Guid promotionId,
        ILayerStoreClient layerStore
    )
    {
        var requestServices = new ServiceCollection()
            .AddSingleton(scope.ContextFactory)
            .AddSingleton(layerStore)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = requestServices };
        httpContext.Request.Headers.Authorization = $"Bearer {nodeToken}";

        var queryProcessor = Substitute.For<IQueryProcessor>();
        var controller = new PipelineController(
            queryProcessor,
            new RepositoryContentAuthorizationService(queryProcessor, new HttpContextAccessor()),
            new ComputeNodeIdentityService(scope.ContextFactory, new PasswordHasherService())
        )
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };

        var content = new byte[] { 1, 2, 3, 4 };
        var artifact = new FormFile(new MemoryStream(content), 0, content.Length, "artifact", "layer.tar");
        return await controller.UploadPromotionArtifact(promotionId, artifact, CancellationToken.None);
    }

    private static async Task<(Guid Id, string Token)> RegisterNodeAsync(OrgComputeTestScope scope, string nodeId)
    {
        var enrollment = await scope.CreateEnrollmentHandler.RunQueryAsync(
            new CreateComputeNodeEnrollmentQuery
            {
                NodeId = nodeId,
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
                NodeId = nodeId,
                EnrollmentToken = enrollment.Get().EnrollmentToken,
            },
            CancellationToken.None
        );
        return (await scope.GetNodeIdAsync(nodeId), register.Get().NodeIdentityToken);
    }

    // Mirrors DependencyLayerPromotionWorker: the running promotion points at its job via
    // LayerStoreObjectKey, and the job has been claimed by a compute node.
    private static async Task<Guid> SeedRunningPromotionAsync(OrgComputeTestScope scope, Guid claimedByNodeId)
    {
        var promotionId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        context.Set<DependencyPromotionRequestEntity>().Add(
            new DependencyPromotionRequestEntity
            {
                Id = promotionId,
                RecipeKey = "npm:left-pad@1.3.0",
                Status = DependencyPromotionRequestStatus.Running,
                RequestedByUserId = Guid.NewGuid(),
                LayerStoreObjectKey = jobId.ToString("D"),
                CreatedAt = DateTimeOffset.UtcNow,
            }
        );
        context.Set<PipelineRunEntity>().Add(
            new PipelineRunEntity
            {
                Id = runId,
                RepositoryId = Guid.Empty,
                Ref = "refs/internal/layer-promotion",
                AfterSha = "npm:left-pad@1.3.0",
                Status = PipelineRunStatus.Running,
                StageOrderJson = "[\"promotion\"]",
                CreatedAt = DateTimeOffset.UtcNow,
            }
        );
        context.Set<PipelineJobEntity>().Add(
            new PipelineJobEntity
            {
                Id = jobId,
                RunId = runId,
                Name = "__layer_promotion__",
                Stage = "promotion",
                RunsOn = "ogb-hosted",
                Status = PipelineJobStatus.Running,
                Script = "echo layer-promotion-complete",
                ClaimedByComputeNodeId = claimedByNodeId,
                CreatedAt = DateTimeOffset.UtcNow,
            }
        );
        await context.SaveChangesAsync();
        return promotionId;
    }
}
