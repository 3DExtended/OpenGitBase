using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.RepositoryMember.Entities;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.RepositoryMember.QueryHandlers;

public class ListRepositoryMemberQueryHandler
    : IQueryHandler<ListRepositoryMemberQuery, IReadOnlyList<RepositoryMemberDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListRepositoryMemberQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<RepositoryMemberDto>>> RunQueryAsync(
        ListRepositoryMemberQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var members = await context
            .Set<RepositoryMemberEntity>()
            .AsNoTracking()
            .Where(x => x.RepositoryId == query.RepositoryId.Value)
            .Join(
                context.Set<UserEntity>().AsNoTracking(),
                member => member.UserId,
                user => user.Id,
                (member, user) =>
                    new RepositoryMemberDto
                    {
                        Id = RepositoryMemberId.From(member.Id),
                        RepositoryId = RepositoryId.From(member.RepositoryId),
                        UserId = UserId.From(member.UserId),
                        Username = user.Username,
                        Role = member.Role,
                    }
            )
            .ToListAsync(cancellationToken);

        var repositoryOwner = await context
            .Set<RepositoryEntity>()
            .AsNoTracking()
            .Where(r => r.Id == query.RepositoryId.Value)
            .Select(r => new { r.OwnerUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (
            repositoryOwner != null
            && repositoryOwner.OwnerUserId != Guid.Empty
            && !members.Exists(m => m.UserId.Value == repositoryOwner.OwnerUserId)
        )
        {
            var ownerUsername = await context
                .Set<UserEntity>()
                .AsNoTracking()
                .Where(user => user.Id == repositoryOwner.OwnerUserId)
                .Select(user => user.Username)
                .FirstOrDefaultAsync(cancellationToken);

            members.Add(
                new RepositoryMemberDto
                {
                    UserId = UserId.From(repositoryOwner.OwnerUserId),
                    Username = ownerUsername,
                    Role = RepositoryRole.Owner,
                    Id = new RepositoryMemberId { Value = Guid.Empty },
                }
            );
        }

        return Option.From<IReadOnlyList<RepositoryMemberDto>>(members);
    }
}
