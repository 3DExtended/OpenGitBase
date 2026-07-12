using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public class ResolveBaseImageBySlugQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ReturnsArtifactForKnownSlug()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PipelineMapsterConfig>(
            typeof(ResolveBaseImageBySlugQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        seedContext.Set<BaseImageCatalogEntity>().Add(
            new BaseImageCatalogEntity
            {
                Id = Guid.NewGuid(),
                Slug = "alpine",
                VersionLabel = "3.20",
                ArtifactUri = "abc123",
                ContentHash = "abc123",
                OciProvenance = "docker.io/library/alpine:3.20",
                CreatedByUserId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
            }
        );
        await seedContext.SaveChangesAsync();

        var handler = scope.GetHandler<ResolveBaseImageBySlugQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ResolveBaseImageBySlugQuery { Slug = "alpine" },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(result, artifact =>
        {
            Assert.Equal("alpine", artifact.Slug);
            Assert.Equal("abc123", artifact.ContentHash);
        });
    }
}
