using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class AddOrganizationMemberQueryHandler : IQueryHandler<AddOrganizationMemberQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public AddOrganizationMemberQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        AddOrganizationMemberQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var exists = await context
            .Set<OrganizationMemberEntity>()
            .AnyAsync(
                x =>
                    x.OrganizationId == query.OrganizationId.Value
                    && x.UserId == query.UserId.Value,
                cancellationToken
            );

        if (exists)
        {
            return Option<Unit>.None;
        }

        var memberId = Guid.NewGuid();
        await context.InsertOnceAsync(
            ct =>
            {
                context
                    .Set<OrganizationMemberEntity>()
                    .Add(
                        new OrganizationMemberEntity
                        {
                            Id = memberId,
                            OrganizationId = query.OrganizationId.Value,
                            UserId = query.UserId.Value,
                            Role = query.Role,
                        }
                    );
                return context.SaveChangesAsync(ct);
            },
            ct => context.Set<OrganizationMemberEntity>().AnyAsync(x => x.Id == memberId, ct),
            cancellationToken
        );
        return Unit.Value;
    }
}
