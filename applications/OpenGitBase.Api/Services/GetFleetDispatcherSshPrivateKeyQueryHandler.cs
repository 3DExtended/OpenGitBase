using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Services;

public sealed class GetFleetDispatcherSshPrivateKeyQueryHandler
    : IQueryHandler<GetFleetDispatcherSshPrivateKeyQuery, string>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IEmailProtectionService _emailProtectionService;

    public GetFleetDispatcherSshPrivateKeyQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService,
        IEmailProtectionService emailProtectionService
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
        _emailProtectionService = emailProtectionService;
    }

    public async Task<Option<string>> RunQueryAsync(
        GetFleetDispatcherSshPrivateKeyQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.FleetBootstrapToken))
        {
            return Option<string>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var secrets = await context
            .Set<FleetSecretsEntity>()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (
            secrets is null
            || string.IsNullOrWhiteSpace(secrets.FleetBootstrapTokenHash)
            || string.IsNullOrWhiteSpace(secrets.DispatcherSshPrivateKeyProtected)
            || !_passwordHasherService.VerifyPassword(
                secrets.FleetBootstrapTokenHash,
                query.FleetBootstrapToken
            )
        )
        {
            return Option<string>.None;
        }

        var privateKey = _emailProtectionService.DecryptEmail(
            secrets.DispatcherSshPrivateKeyProtected
        );
        secrets.FleetBootstrapTokenHash = string.Empty;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(privateKey);
    }
}
