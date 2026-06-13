using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.RepositoryMember;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.RepositoryMember.Entities;
using OpenGitBase.Features.RepositoryMember.QueryHandlers;
using OpenGitBase.Features.RepositoryMember.Tests.Testing;

namespace OpenGitBase.Features.RepositoryMember.Tests.QueryHandlers;

public class CreateRepositoryMemberQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_PersistsEntity_ReturnsNewId()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (repositoryId, ownerUserId) = await RepositoryMemberTestData.SeedRepositoryAsync(
            seedContext
        );

        var handler = scope.GetHandler<CreateRepositoryMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateRepositoryMemberQuery
            {
                ModelToCreate = new RepositoryMemberDto
                {
                    RepositoryId = repositoryId,
                    UserId = ownerUserId,
                    Role = RepositoryRole.Admin,
                },
            },
            CancellationToken.None
        );

        var id = QueryHandlerResultAssert.AssertSome(result);
        QueryHandlerResultAssert.AssertIdentifierNonEmpty(id);

        await using var context = await scope.CreateDbContextAsync();
        var entity = await context.Set<RepositoryMemberEntity>().FindAsync(id.Value);
        Assert.NotNull(entity);
        Assert.Equal(id.Value, entity.Id);
        Assert.Equal(repositoryId.Value, entity.RepositoryId);
        Assert.Equal(ownerUserId.Value, entity.UserId);
        Assert.Equal(RepositoryRole.Admin, entity.Role);
    }

    private static InMemoryFeatureTestScope<
        OpenGitBaseDbContext,
        RepositoryMemberMapsterConfig
    > CreateScope() =>
        new(
            typeof(CreateRepositoryMemberQueryHandler).Assembly,
            typeof(RepositoryMapsterConfig).Assembly
        );
}
