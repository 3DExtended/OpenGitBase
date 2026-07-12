using Microsoft.EntityFrameworkCore;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public class CiVariableComposerHandlerTests
{
    [Fact]
    public async Task RequestPromotion_UsesMostRecentFiveOutcomes()
    {
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        var recipeKey = "recipe-key";
        var now = DateTimeOffset.UtcNow;
        context.Set<DependencyInstallOutcomeEntity>()
            .AddRange(
                Outcome(recipeKey, false, now.AddMinutes(-10)),
                Outcome(recipeKey, true, now.AddMinutes(-9)),
                Outcome(recipeKey, true, now.AddMinutes(-8)),
                Outcome(recipeKey, true, now.AddMinutes(-7)),
                Outcome(recipeKey, true, now.AddMinutes(-6)),
                Outcome(recipeKey, true, now.AddMinutes(-5))
            );
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
    }

    [Fact]
    public async Task RequestPromotion_BlocksWhenRecentOutcomeFailed()
    {
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        var recipeKey = "recipe-key-fail";
        var now = DateTimeOffset.UtcNow;
        context.Set<DependencyInstallOutcomeEntity>()
            .AddRange(
                Outcome(recipeKey, true, now.AddMinutes(-9)),
                Outcome(recipeKey, true, now.AddMinutes(-8)),
                Outcome(recipeKey, true, now.AddMinutes(-7)),
                Outcome(recipeKey, true, now.AddMinutes(-6)),
                Outcome(recipeKey, false, now.AddMinutes(-5)),
                Outcome(recipeKey, true, now.AddMinutes(-10))
            );
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

        Assert.True(result.IsNone);
    }

    private static DependencyInstallOutcomeEntity Outcome(
        string recipeKey,
        bool success,
        DateTimeOffset createdAt
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            RecipeKey = recipeKey,
            Success = success,
            ExitCode = success ? 0 : 1,
            DurationMs = 10,
            CreatedAt = createdAt,
        };
}
