using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.RepositoryMember.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.RepositoryMember.QueryHandlers;

public class ListRepositoryMemberQueryHandler
    : IQueryHandler<ListRepositoryMemberQuery, IReadOnlyList<RepositoryMemberDto>>
{
    private readonly IMapper _mapper;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListRepositoryMemberQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _mapper = mapper;
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<RepositoryMemberDto>>> RunQueryAsync(
        ListRepositoryMemberQuery query,
        CancellationToken cancellationToken
    )
    {
        using (
            var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false)
        )
        {
            var databaseQuery = context
                .Set<RepositoryMemberEntity>()
                .AsNoTracking()
                .Where(x => x.RepositoryId == query.RepositoryId.Value);

            var entities = await databaseQuery.ToListAsync(cancellationToken).ConfigureAwait(false);

            var result = new List<RepositoryMemberDto>();
            if (entities.Count != 0)
            {
                result.AddRange(entities.Select(_mapper.Map<RepositoryMemberDto>));
            }

            // add owner of repository as a member with Owner role
            var repositoryOwnerId = await context
                .Set<RepositoryEntity>()
                .Where(r => r.Id == query.RepositoryId.Value)
                .Select(r => r.OwnerUserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (
                repositoryOwnerId != Guid.Empty
                && !result.Exists(m => m.UserId.Value == repositoryOwnerId)
            )
            {
                result.Add(
                    new RepositoryMemberDto
                    {
                        UserId = UserId.From(repositoryOwnerId),
                        Role = RepositoryRole.Owner,
                        Id = new RepositoryMemberId { Value = Guid.Empty },
                    }
                );
            }

            return Option.From((IReadOnlyList<RepositoryMemberDto>)result);
        }
    }
}
