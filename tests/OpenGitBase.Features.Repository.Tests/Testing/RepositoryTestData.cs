using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.Tests.Testing;

public static class RepositoryTestData
{
    public static UserEntity CreateUser(Guid id, string username) =>
        new()
        {
            Id = id,
            Username = username,
            NormalizedUsername = username.ToLowerInvariant(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

    public static async Task<(RepositoryId Id, UserId OwnerUserId)> SeedPublicRepositoryAsync(
        OpenGitBaseDbContext context,
        string ownerUsername = "demo-user",
        string slug = "hello-world",
        string name = "Hello World",
        Guid? ownerUserId = null
    )
    {
        var ownerId = ownerUserId ?? Guid.NewGuid();
        context.Set<UserEntity>().Add(CreateUser(ownerId, ownerUsername));
        var repositoryId = Guid.NewGuid();
        context
            .Set<RepositoryEntity>()
            .Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = name,
                    Slug = slug,
                    OwnerUserId = ownerId,
                    PhysicalPath = $"./repositories/{ownerId}/{slug}",
                    IsPrivate = false,
                    StorageBytesUsed = 1024,
                }
            );
        await context.SaveChangesAsync().ConfigureAwait(false);
        return (RepositoryId.From(repositoryId), UserId.From(ownerId));
    }

    public static async Task<(OrganizationId Id, RepositoryId RepositoryId)> SeedOrganizationOwnedRepositoryAsync(
        OpenGitBaseDbContext context,
        string orgSlug = "acme-corp",
        string repoSlug = "public-app"
    )
    {
        var orgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var repositoryId = Guid.NewGuid();

        context.Set<UserEntity>().Add(CreateUser(ownerUserId, "org-owner"));
        context
            .Set<OrganizationEntity>()
            .Add(
                new OrganizationEntity
                {
                    Id = orgId,
                    Name = "Acme Corp",
                    Slug = orgSlug,
                    OwnerUserId = ownerUserId,
                }
            );
        context
            .Set<RepositoryEntity>()
            .Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "Public App",
                    Slug = repoSlug,
                    OwnerUserId = orgId,
                    PhysicalPath = $"./repositories/{orgId}/{repoSlug}",
                    IsPrivate = false,
                }
            );
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;").ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return (OrganizationId.From(orgId), RepositoryId.From(repositoryId));
    }
}
