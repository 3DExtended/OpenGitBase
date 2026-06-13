using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.RepositoryMember.Entities;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.RepositoryMember.Tests.Testing;

public static class RepositoryMemberTestData
{
    public static async Task<RepositoryMemberSeedData> SeedRepositoryWithMemberAsync(
        OpenGitBaseDbContext context,
        RepositoryRole role = RepositoryRole.Writer
    )
    {
        var ownerUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var repositoryId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        context
            .Set<UserEntity>()
            .AddRange(CreateUser(ownerUserId, "owner"), CreateUser(memberUserId, "member"));
        context
            .Set<RepositoryEntity>()
            .Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "Test repository",
                    OwnerUserId = ownerUserId,
                    Slug = "test-repo",
                    PhysicalPath = $"./repositories/{ownerUserId}/test-repo",
                }
            );

        var memberEntity = new RepositoryMemberEntity
        {
            Id = memberId,
            RepositoryId = repositoryId,
            UserId = memberUserId,
            Role = role,
        };
        context.Set<RepositoryMemberEntity>().Add(memberEntity);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return new RepositoryMemberSeedData(
            RepositoryId.From(repositoryId),
            UserId.From(ownerUserId),
            UserId.From(memberUserId),
            RepositoryMemberId.From(memberId),
            memberEntity
        );
    }

    public static async Task<(RepositoryId RepositoryId, UserId OwnerUserId)> SeedRepositoryAsync(
        OpenGitBaseDbContext context
    )
    {
        var ownerUserId = Guid.NewGuid();
        var repositoryId = Guid.NewGuid();

        context.Set<UserEntity>().Add(CreateUser(ownerUserId, "owner"));
        context
            .Set<RepositoryEntity>()
            .Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "Owner-only repository",
                    OwnerUserId = ownerUserId,
                    Slug = "owner-only",
                    PhysicalPath = $"./repositories/{ownerUserId}/owner-only",
                }
            );
        await context.SaveChangesAsync().ConfigureAwait(false);

        return (RepositoryId.From(repositoryId), UserId.From(ownerUserId));
    }

    public static async Task<RepositoryMemberId> SeedMemberAsync(
        OpenGitBaseDbContext context,
        RepositoryId repositoryId,
        UserId userId,
        RepositoryRole role = RepositoryRole.Admin
    )
    {
        var memberId = Guid.NewGuid();
        context
            .Set<RepositoryMemberEntity>()
            .Add(
                new RepositoryMemberEntity
                {
                    Id = memberId,
                    RepositoryId = repositoryId.Value,
                    UserId = userId.Value,
                    Role = role,
                }
            );
        await context.SaveChangesAsync().ConfigureAwait(false);
        return RepositoryMemberId.From(memberId);
    }

    private static UserEntity CreateUser(Guid id, string username) =>
        new()
        {
            Id = id,
            Username = username,
            NormalizedUsername = username.ToUpperInvariant(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
}
