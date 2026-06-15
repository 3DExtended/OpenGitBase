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

public class ListRepositoryMemberQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenNoMembersAndRepositoryMissing_ReturnsEmptyList()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<ListRepositoryMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListRepositoryMemberQuery { RepositoryId = RepositoryId.From(Guid.NewGuid()) },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(result, list => Assert.Empty(list));
    }

    [Fact]
    public async Task RunQueryAsync_WhenRepositoryHasNoMembers_ReturnsSyntheticOwner()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (repositoryId, ownerUserId) = await RepositoryMemberTestData.SeedRepositoryAsync(
            seedContext
        );

        var handler = scope.GetHandler<ListRepositoryMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListRepositoryMemberQuery { RepositoryId = repositoryId },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            list =>
            {
                var owner = Assert.Single(list);
                Assert.Equal(ownerUserId, owner.UserId);
                Assert.Equal("owner", owner.Username);
                Assert.Equal(RepositoryRole.Owner, owner.Role);
                Assert.Equal(Guid.Empty, owner.Id.Value);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenSeeded_ReturnsMembersForRepository()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var seed = await RepositoryMemberTestData.SeedRepositoryWithMemberAsync(seedContext);

        var handler = scope.GetHandler<ListRepositoryMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListRepositoryMemberQuery { RepositoryId = seed.RepositoryId },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            list =>
            {
                Assert.Equal(2, list.Count);

                var member = Assert.Single(list, item => item.Id == seed.MemberId);
                Assert.Equal(seed.MemberUserId, member.UserId);
                Assert.Equal("member", member.Username);
                Assert.Equal(RepositoryRole.Writer, member.Role);
                Assert.Equal(seed.RepositoryId, member.RepositoryId);

                var owner = Assert.Single(list, item => item.Role == RepositoryRole.Owner);
                Assert.Equal(seed.OwnerUserId, owner.UserId);
                Assert.Equal("owner", owner.Username);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenOwnerAlreadyListed_DoesNotDuplicateOwner()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var seed = await RepositoryMemberTestData.SeedRepositoryWithMemberAsync(
            seedContext,
            RepositoryRole.Admin
        );
        await RepositoryMemberTestData.SeedMemberAsync(
            seedContext,
            seed.RepositoryId,
            seed.OwnerUserId,
            RepositoryRole.Admin
        );

        var handler = scope.GetHandler<ListRepositoryMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListRepositoryMemberQuery { RepositoryId = seed.RepositoryId },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            list =>
            {
                Assert.Equal(2, list.Count);
                Assert.Single(list, item => item.UserId == seed.OwnerUserId);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_FiltersByRepositoryId()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var firstRepository = await RepositoryMemberTestData.SeedRepositoryWithMemberAsync(
            seedContext
        );
        var secondRepository = await RepositoryMemberTestData.SeedRepositoryWithMemberAsync(
            seedContext
        );

        var handler = scope.GetHandler<ListRepositoryMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListRepositoryMemberQuery { RepositoryId = firstRepository.RepositoryId },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            list =>
            {
                Assert.Equal(2, list.Count);
                Assert.DoesNotContain(
                    list,
                    item => item.RepositoryId == secondRepository.RepositoryId
                );
                Assert.Contains(list, item => item.Id == firstRepository.MemberId);
            }
        );
    }

    private static InMemoryFeatureTestScope<
        OpenGitBaseDbContext,
        RepositoryMemberMapsterConfig
    > CreateScope() =>
        new(
            typeof(ListRepositoryMemberQueryHandler).Assembly,
            typeof(RepositoryMapsterConfig).Assembly
        );
}
