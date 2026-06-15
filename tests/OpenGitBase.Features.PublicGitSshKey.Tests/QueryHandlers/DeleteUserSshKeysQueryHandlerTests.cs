using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.PublicGitSshKey;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.PublicGitSshKey.Entities;
using OpenGitBase.Features.PublicGitSshKey.QueryHandlers;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.PublicGitSshKey.Tests.QueryHandlers;

public class DeleteUserSshKeysQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_RemovesAllKeysForUser()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PublicGitSshKeyMapsterConfig>(
            typeof(DeleteUserSshKeysQueryHandler).Assembly,
            typeof(UserEntity).Assembly
        );
        await scope.EnsureCreatedAsync();

        var ownerUserId = Guid.NewGuid();
        await using (var seedContext = await scope.CreateDbContextAsync())
        {
            seedContext
                .Set<UserEntity>()
                .Add(
                    new UserEntity
                    {
                        Id = ownerUserId,
                        Username = "key-owner",
                        NormalizedUsername = "key-owner",
                        CreatedAt = DateTimeOffset.UtcNow,
                    }
                );
            seedContext
                .Set<PublicGitSshKeyEntity>()
                .Add(
                    new PublicGitSshKeyEntity
                    {
                        Id = Guid.NewGuid(),
                        OwnerUserId = ownerUserId,
                        Name = "laptop",
                        PublicSSHKey = "ssh-rsa AAA",
                        Fingerprint = "fp-1",
                    }
                );
            await seedContext.SaveChangesAsync();
        }

        var handler = scope.GetHandler<DeleteUserSshKeysQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DeleteUserSshKeysQuery
            {
                UserId = OpenGitBase.Features.Users.Contracts.Models.UserId.From(ownerUserId),
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);

        await using var verifyContext = await scope.CreateDbContextAsync();
        var remaining = await verifyContext
            .Set<PublicGitSshKeyEntity>()
            .CountAsync(x => x.OwnerUserId == ownerUserId);
        Assert.Equal(0, remaining);
    }
}
