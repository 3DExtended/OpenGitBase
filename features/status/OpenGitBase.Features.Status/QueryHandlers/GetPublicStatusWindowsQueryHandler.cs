using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Services;

namespace OpenGitBase.Features.Status.QueryHandlers;

public sealed class GetPublicStatusWindowsQueryHandler
    : IQueryHandler<GetPublicStatusWindowsQuery, List<PublicStatusOutageWindowDto>>
{
    private readonly StatusOutageWindowService _outageWindowService;

    public GetPublicStatusWindowsQueryHandler(StatusOutageWindowService outageWindowService)
    {
        _outageWindowService = outageWindowService;
    }

    public async Task<Option<List<PublicStatusOutageWindowDto>>> RunQueryAsync(
        GetPublicStatusWindowsQuery query,
        CancellationToken cancellationToken
    )
    {
        var windows = await _outageWindowService
            .ListPublicWindowsAsync(query.Days, cancellationToken)
            .ConfigureAwait(false);
        return Option.From(windows);
    }
}
