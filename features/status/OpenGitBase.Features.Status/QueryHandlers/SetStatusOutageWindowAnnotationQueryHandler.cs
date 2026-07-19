using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Services;

namespace OpenGitBase.Features.Status.QueryHandlers;

public sealed class SetStatusOutageWindowAnnotationQueryHandler
    : IQueryHandler<SetStatusOutageWindowAnnotationQuery, AdminStatusOutageWindowDto?>
{
    private readonly StatusOutageWindowService _outageWindowService;

    public SetStatusOutageWindowAnnotationQueryHandler(
        StatusOutageWindowService outageWindowService
    )
    {
        _outageWindowService = outageWindowService;
    }

    public async Task<Option<AdminStatusOutageWindowDto?>> RunQueryAsync(
        SetStatusOutageWindowAnnotationQuery query,
        CancellationToken cancellationToken
    )
    {
        var updated = await _outageWindowService
            .SetAnnotationAsync(query.WindowId, query.Annotation, cancellationToken)
            .ConfigureAwait(false);
        return Option.From<AdminStatusOutageWindowDto?>(updated);
    }
}
