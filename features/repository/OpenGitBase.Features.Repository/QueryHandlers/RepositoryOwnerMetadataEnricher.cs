using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

internal static class RepositoryOwnerMetadataEnricher
{
    public static async Task EnrichAsync(
        IReadOnlyList<RepositoryDto> repositories,
        OpenGitBaseDbContext context,
        CancellationToken cancellationToken
    )
    {
        if (repositories.Count == 0)
        {
            return;
        }

        var ownerIds = repositories.Select(repo => repo.OwnerUserId.Value).Distinct().ToList();

        var userSlugs = new Dictionary<Guid, string>();
        if (context.Model.FindEntityType(typeof(UserEntity)) != null)
        {
            var users = await context
                .Set<UserEntity>()
                .AsNoTracking()
                .Where(user => ownerIds.Contains(user.Id))
                .Select(user => new { user.Id, user.Username })
                .ToListAsync(cancellationToken);
            userSlugs = users.ToDictionary(user => user.Id, user => user.Username);
        }

        var organizationSlugs = new Dictionary<Guid, string>();
        if (context.Model.FindEntityType(typeof(OrganizationEntity)) != null)
        {
            var organizations = await context
                .Set<OrganizationEntity>()
                .AsNoTracking()
                .Where(org => ownerIds.Contains(org.Id))
                .Select(org => new { org.Id, org.Slug })
                .ToListAsync(cancellationToken);
            organizationSlugs = organizations.ToDictionary(org => org.Id, org => org.Slug);
        }

        foreach (var repository in repositories)
        {
            var ownerId = repository.OwnerUserId.Value;
            if (organizationSlugs.TryGetValue(ownerId, out var organizationSlug))
            {
                repository.OwnerKind = "organization";
                repository.OwnerSlug = organizationSlug;
                continue;
            }

            if (userSlugs.TryGetValue(ownerId, out var username))
            {
                repository.OwnerKind = "user";
                repository.OwnerSlug = username;
            }
        }
    }
}
