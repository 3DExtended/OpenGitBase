using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.RepositoryMember;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.RepositoryMember.Entities;
using OpenGitBase.Features.RepositoryMember.QueryHandlers;
using OpenGitBase.Features.RepositoryMember.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.RepositoryMember.Tests.QueryHandlers;

public class DeleteUserRepositoryMembershipsQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_RemovesAllMembershipsForUser()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMemberMapsterConfig>(
            typeof(DeleteUserRepositoryMembershipsQueryHandler).Assembly,
            typeof(global::OpenGitBase.Features.Repository.RepositoryMapsterConfig).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var seed = await RepositoryMemberTestData.SeedRepositoryWithMemberAsync(seedContext);

        var handler = scope.GetHandler<DeleteUserRepositoryMembershipsQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DeleteUserRepositoryMembershipsQuery { UserId = seed.MemberUserId },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);

        await using var verifyContext = await scope.CreateDbContextAsync();
        var remaining = await verifyContext
            .Set<RepositoryMemberEntity>()
            .CountAsync(x => x.UserId == seed.MemberUserId.Value);
        Assert.Equal(0, remaining);
    }
}
