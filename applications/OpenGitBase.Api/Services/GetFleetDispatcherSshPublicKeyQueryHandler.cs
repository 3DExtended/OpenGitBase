using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Services;

public sealed class GetFleetDispatcherSshPublicKeyQueryHandler
    : IQueryHandler<GetFleetDispatcherSshPublicKeyQuery, string>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetFleetDispatcherSshPublicKeyQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<string>> RunQueryAsync(
        GetFleetDispatcherSshPublicKeyQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var secrets = await context
            .Set<FleetSecretsEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(secrets?.DispatcherSshPublicKey)
            ? Option<string>.None
            : Option.From(secrets.DispatcherSshPublicKey);
    }
}
