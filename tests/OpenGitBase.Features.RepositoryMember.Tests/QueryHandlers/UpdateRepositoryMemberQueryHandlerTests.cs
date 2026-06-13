using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.RepositoryMember.Entities;
using OpenGitBase.Features.RepositoryMember.QueryHandlers;
using OpenGitBase.Features.RepositoryMember.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.RepositoryMember.Tests.QueryHandlers;

public class UpdateRepositoryMemberQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_UpdatesAndReturnsUnit()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var seed = await RepositoryMemberTestData.SeedRepositoryWithMemberAsync(seedContext);

        var handler = scope.GetHandler<UpdateRepositoryMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new UpdateRepositoryMemberQuery
            {
                UpdatedModel = new RepositoryMemberDto
                {
                    Id = seed.MemberId,
                    RepositoryId = seed.RepositoryId,
                    UserId = seed.MemberUserId,
                    Role = RepositoryRole.Admin,
                },
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);

        await using var verifyContext = await scope.CreateDbContextAsync();
        var entity = await verifyContext
            .Set<RepositoryMemberEntity>()
            .FindAsync(seed.MemberId.Value);
        Assert.NotNull(entity);
        Assert.Equal(RepositoryRole.Admin, entity.Role);
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<UpdateRepositoryMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new UpdateRepositoryMemberQuery
            {
                UpdatedModel = new RepositoryMemberDto
                {
                    Id = RepositoryMemberId.From(Guid.NewGuid()),
                    RepositoryId = RepositoryId.From(Guid.NewGuid()),
                    UserId = UserId.From(Guid.NewGuid()),
                    Role = RepositoryRole.Admin,
                },
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
            typeof(UpdateRepositoryMemberQueryHandler).Assembly,
            typeof(RepositoryMapsterConfig).Assembly
        );
}
