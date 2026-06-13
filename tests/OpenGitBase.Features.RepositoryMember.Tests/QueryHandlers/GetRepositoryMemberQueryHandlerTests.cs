using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.RepositoryMember.QueryHandlers;
using OpenGitBase.Features.RepositoryMember.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.RepositoryMember.Tests.QueryHandlers;

public class GetRepositoryMemberQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_ReturnsMappedDto()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var seed = await RepositoryMemberTestData.SeedRepositoryWithMemberAsync(seedContext);

        var handler = scope.GetHandler<GetRepositoryMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetRepositoryMemberQuery
            {
                RepositoryId = seed.RepositoryId,
                UserId = seed.MemberUserId,
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            dto =>
            {
                Assert.Equal(seed.MemberId, dto.Id);
                Assert.Equal(seed.RepositoryId, dto.RepositoryId);
                Assert.Equal(seed.MemberUserId, dto.UserId);
                Assert.Equal(RepositoryRole.Writer, dto.Role);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var seed = await RepositoryMemberTestData.SeedRepositoryWithMemberAsync(seedContext);

        var handler = scope.GetHandler<GetRepositoryMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetRepositoryMemberQuery
            {
                RepositoryId = seed.RepositoryId,
                UserId = UserId.From(Guid.NewGuid()),
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }

    [Fact]
    public async Task RunQueryAsync_WhenRepositoryIdMismatch_ReturnsNone()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var seed = await RepositoryMemberTestData.SeedRepositoryWithMemberAsync(seedContext);

        var handler = scope.GetHandler<GetRepositoryMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetRepositoryMemberQuery
            {
                RepositoryId = RepositoryId.From(Guid.NewGuid()),
                UserId = seed.MemberUserId,
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }

    private static InMemoryFeatureTestScope<
        OpenGitBaseDbContext,
        RepositoryMemberMapsterConfig
    > CreateScope() =>
        new(
            typeof(GetRepositoryMemberQueryHandler).Assembly,
            typeof(RepositoryMapsterConfig).Assembly
        );
}
