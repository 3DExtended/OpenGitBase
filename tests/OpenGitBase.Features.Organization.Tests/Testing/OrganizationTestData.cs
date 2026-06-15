using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Organization.Tests.Testing;

public static class OrganizationTestData
{
    public const string SampleName = "Sample";
    public const string UpdatedName = "Updated";

    public const string SampleSlug = "sample-org";

    public static UserEntity CreateUser(Guid id, string username = "org-owner") =>
        new()
        {
            Id = id,
            Username = username,
            NormalizedUsername = username.ToLowerInvariant(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

    public static async Task<(OrganizationId Id, OrganizationEntity Entity, UserId OwnerUserId)> SeedAsync(
        OpenGitBaseDbContext context,
        Guid? ownerUserId = null,
        string slug = SampleSlug
    )
    {
        var ownerId = ownerUserId ?? Guid.NewGuid();
        context.Set<UserEntity>().Add(CreateUser(ownerId));

        var id = Guid.NewGuid();
        var entity = new OrganizationEntity
        {
            Id = id,
            Name = SampleName,
            Slug = slug,
            OwnerUserId = ownerId,
        };
        context.Set<OrganizationEntity>().Add(entity);
        await context.SaveChangesAsync().ConfigureAwait(false);
        return (OrganizationId.From(id), entity, UserId.From(ownerId));
    }

    public static async Task SeedMemberAsync(
        OpenGitBaseDbContext context,
        OrganizationId organizationId,
        UserId userId,
        OrganizationMemberRole role = OrganizationMemberRole.Member
    )
    {
        if (!await context.Set<UserEntity>().AnyAsync(x => x.Id == userId.Value).ConfigureAwait(false))
        {
            context.Set<UserEntity>().Add(CreateUser(userId.Value, $"user-{userId.Value:N}"[..12]));
        }

        context
            .Set<OrganizationMemberEntity>()
            .Add(
                new OrganizationMemberEntity
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId.Value,
                    UserId = userId.Value,
                    Role = role,
                }
            );
        await context.SaveChangesAsync().ConfigureAwait(false);
    }
}
