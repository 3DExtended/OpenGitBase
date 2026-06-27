#pragma warning disable SA1402 // File may only contain a single type
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class CreateProtectedBranchRuleQueryHandler
    : IQueryHandler<CreateProtectedBranchRuleQuery, ProtectedBranchRuleId>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public CreateProtectedBranchRuleQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<ProtectedBranchRuleId>> RunQueryAsync(
        CreateProtectedBranchRuleQuery query,
        CancellationToken cancellationToken
    )
    {
        var model = query.ModelToCreate;
        if (!ProtectedBranchRuleMapper.IsValidModel(model))
        {
            return Option<ProtectedBranchRuleId>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var repositoryExists = await context
            .Set<RepositoryEntity>()
            .AnyAsync(entity => entity.Id == model.RepositoryId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (!repositoryExists)
        {
            return Option<ProtectedBranchRuleId>.None;
        }

        var entity = ProtectedBranchRuleMapper.ToEntity(model);
        context.Set<ProtectedBranchRuleEntity>().Add(entity);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            return Option<ProtectedBranchRuleId>.None;
        }

        return Option.From(ProtectedBranchRuleId.From(entity.Id));
    }
}

public class GetProtectedBranchRuleQueryHandler
    : IQueryHandler<GetProtectedBranchRuleQuery, ProtectedBranchRuleDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetProtectedBranchRuleQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<ProtectedBranchRuleDto>> RunQueryAsync(
        GetProtectedBranchRuleQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await ProtectedBranchRuleMapper
            .BaseQuery(context)
            .FirstOrDefaultAsync(item => item.Id == query.ModelId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return Option<ProtectedBranchRuleDto>.None;
        }

        return Option.From(ProtectedBranchRuleMapper.ToDto(entity));
    }
}

public class ListProtectedBranchRulesQueryHandler
    : IQueryHandler<ListProtectedBranchRulesQuery, IReadOnlyList<ProtectedBranchRuleDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListProtectedBranchRulesQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<ProtectedBranchRuleDto>>> RunQueryAsync(
        ListProtectedBranchRulesQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = await ProtectedBranchRuleMapper
            .BaseQuery(context)
            .Where(item => item.RepositoryId == query.RepositoryId.Value)
            .OrderBy(item => item.Pattern)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Option.From(
            (IReadOnlyList<ProtectedBranchRuleDto>)items
                .Select(ProtectedBranchRuleMapper.ToDto)
                .ToList()
        );
    }
}

public class UpdateProtectedBranchRuleQueryHandler
    : IQueryHandler<UpdateProtectedBranchRuleQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public UpdateProtectedBranchRuleQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        UpdateProtectedBranchRuleQuery query,
        CancellationToken cancellationToken
    )
    {
        var model = query.UpdatedModel;
        if (!ProtectedBranchRuleMapper.IsValidModel(model))
        {
            return Option<Unit>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await context
            .Set<ProtectedBranchRuleEntity>()
            .FirstOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null || entity.RepositoryId != model.RepositoryId.Value)
        {
            return Option<Unit>.None;
        }

        ProtectedBranchRuleMapper.ApplyUpdate(entity, model);

        try
        {
            await context
                .Set<ProtectedBranchAllowedUserEntity>()
                .Where(item => item.ProtectedBranchRuleId == entity.Id)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            await context
                .Set<PushRuleEntity>()
                .Where(item => item.ProtectedBranchRuleId == entity.Id)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            var replacement = ProtectedBranchRuleMapper.ToEntity(model);
            replacement.Id = entity.Id;
            replacement.RepositoryId = entity.RepositoryId;
            context.Set<ProtectedBranchAllowedUserEntity>().AddRange(replacement.AllowedUsers);
            context.Set<PushRuleEntity>().AddRange(replacement.PushRules);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            return Option<Unit>.None;
        }

        return Option.From(Unit.Value);
    }
}

public class DeleteProtectedBranchRuleQueryHandler
    : IQueryHandler<DeleteProtectedBranchRuleQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public DeleteProtectedBranchRuleQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        DeleteProtectedBranchRuleQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await context
            .Set<ProtectedBranchRuleEntity>()
            .FirstOrDefaultAsync(item => item.Id == query.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return Option<Unit>.None;
        }

        context.Set<ProtectedBranchRuleEntity>().Remove(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(Unit.Value);
    }
}
