using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.PublicGitSshKey.Entities;

namespace OpenGitBase.Features.PublicGitSshKey.QueryHandlers;

public class GetPublicGitSshKeyByFingerprintQueryHandler
    : IQueryHandler<GetPublicGitSshKeyByFingerprintQuery, PublicGitSshKeyDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public GetPublicGitSshKeyByFingerprintQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<PublicGitSshKeyDto>> RunQueryAsync(
        GetPublicGitSshKeyByFingerprintQuery query,
        CancellationToken cancellationToken
    )
    {
        var lookupCandidates = SshKeyFingerprintNormalizer.GetLookupCandidates(query.Fingerprint);
        if (lookupCandidates.Count == 0)
        {
            return Option<PublicGitSshKeyDto>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context
            .Set<PublicGitSshKeyEntity>()
            .AsNoTracking()
            .Where(key => lookupCandidates.Contains(key.Fingerprint))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity == null
            ? Option<PublicGitSshKeyDto>.None
            : Option.From(_mapper.Map<PublicGitSshKeyDto>(entity));
    }
}
