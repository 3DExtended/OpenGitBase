using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class UpdateRepositoryDefaultBranchQueryHandler
    : IQueryHandler<UpdateRepositoryDefaultBranchQuery, RepositoryDto>
{
    private readonly IMapper _mapper;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public UpdateRepositoryDefaultBranchQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _mapper = mapper;
        _contextFactory = contextFactory;
    }

    public async Task<Option<RepositoryDto>> RunQueryAsync(
        UpdateRepositoryDefaultBranchQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.DefaultBranchName))
        {
            return Option<RepositoryDto>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await context
            .Set<RepositoryEntity>()
            .FirstOrDefaultAsync(item => item.Id == query.RepositoryId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return Option<RepositoryDto>.None;
        }

        var branchName = query.DefaultBranchName.Trim();
        if (
            !query.AllowMissingBranch
            && query.KnownBranchNames is { Count: > 0 }
            && !query.KnownBranchNames.Any(name =>
                string.Equals(name, branchName, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return Option<RepositoryDto>.None;
        }

        entity.DefaultBranchName = branchName;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(_mapper.Map<RepositoryDto>(entity));
    }
}
