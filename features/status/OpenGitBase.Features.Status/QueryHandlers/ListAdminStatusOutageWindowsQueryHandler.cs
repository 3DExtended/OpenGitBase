using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Services;

namespace OpenGitBase.Features.Status.QueryHandlers;

public sealed class ListAdminStatusOutageWindowsQueryHandler
    : IQueryHandler<ListAdminStatusOutageWindowsQuery, List<AdminStatusOutageWindowDto>>
{
    private readonly StatusOutageWindowService _outageWindowService;

    public ListAdminStatusOutageWindowsQueryHandler(StatusOutageWindowService outageWindowService)
    {
        _outageWindowService = outageWindowService;
    }

    public async Task<Option<List<AdminStatusOutageWindowDto>>> RunQueryAsync(
        ListAdminStatusOutageWindowsQuery query,
        CancellationToken cancellationToken
    )
    {
        var windows = await _outageWindowService
            .ListAdminWindowsAsync(cancellationToken)
            .ConfigureAwait(false);
        return Option.From(windows);
    }
}
