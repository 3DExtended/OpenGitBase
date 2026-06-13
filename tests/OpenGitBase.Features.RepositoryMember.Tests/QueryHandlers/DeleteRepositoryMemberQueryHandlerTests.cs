using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.RepositoryMember;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.RepositoryMember.Entities;
using OpenGitBase.Features.RepositoryMember.QueryHandlers;
using OpenGitBase.Features.RepositoryMember.Tests.Testing;

namespace OpenGitBase.Features.RepositoryMember.Tests.QueryHandlers;

public class DeleteRepositoryMemberQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_DeletesAndReturnsUnit()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var seed = await RepositoryMemberTestData.SeedRepositoryWithMemberAsync(seedContext);

        var handler = scope.GetHandler<DeleteRepositoryMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DeleteRepositoryMemberQuery { Id = seed.MemberId },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);

        await using var verifyContext = await scope.CreateDbContextAsync();
        Assert.Empty(verifyContext.Set<RepositoryMemberEntity>());
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<DeleteRepositoryMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DeleteRepositoryMemberQuery { Id = RepositoryMemberId.From(Guid.NewGuid()) },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }

    private static InMemoryFeatureTestScope<
        OpenGitBaseDbContext,
        RepositoryMemberMapsterConfig
    > CreateScope() =>
        new(
            typeof(DeleteRepositoryMemberQueryHandler).Assembly,
            typeof(RepositoryMapsterConfig).Assembly
        );
}
