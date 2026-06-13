using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.RepositoryMember.Entities;

namespace OpenGitBase.Features.RepositoryMember.QueryHandlers;

public class GetRepositoryMemberQueryHandler
    : IQueryHandler<GetRepositoryMemberQuery, RepositoryMemberDto>
{
    private readonly IMapper _mapper;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetRepositoryMemberQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _mapper = mapper;
        _contextFactory = contextFactory;
    }

    public async Task<Option<RepositoryMemberDto>> RunQueryAsync(
        GetRepositoryMemberQuery query,
        CancellationToken cancellationToken
    )
    {
        using (
            var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false)
        )
        {
            var databaseQuery = context.Set<RepositoryMemberEntity>().AsNoTracking();

            var entity = await databaseQuery
                .FirstOrDefaultAsync(
                    cp =>
                        cp.UserId == query.UserId.Value
                        && cp.RepositoryId == query.RepositoryId.Value,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return entity == null
                ? Option.None
                : Option.From(_mapper.Map<RepositoryMemberDto>(entity));
        }
    }
}
