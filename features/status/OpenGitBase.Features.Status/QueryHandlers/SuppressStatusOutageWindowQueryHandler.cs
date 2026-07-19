using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Services;

namespace OpenGitBase.Features.Status.QueryHandlers;

public sealed class SuppressStatusOutageWindowQueryHandler
    : IQueryHandler<SuppressStatusOutageWindowQuery, AdminStatusOutageWindowDto?>
{
    private readonly StatusOutageWindowService _outageWindowService;

    public SuppressStatusOutageWindowQueryHandler(StatusOutageWindowService outageWindowService)
    {
        _outageWindowService = outageWindowService;
    }

    public async Task<Option<AdminStatusOutageWindowDto?>> RunQueryAsync(
        SuppressStatusOutageWindowQuery query,
        CancellationToken cancellationToken
    )
    {
        var updated = await _outageWindowService
            .SetSuppressedAsync(query.WindowId, query.Suppressed, cancellationToken)
            .ConfigureAwait(false);
        return Option.From<AdminStatusOutageWindowDto?>(updated);
    }
}
