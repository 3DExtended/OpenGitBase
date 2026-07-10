using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Services;

namespace OpenGitBase.Features.Status.QueryHandlers;

public sealed class GetPublicStatusHistoryQueryHandler
    : IQueryHandler<GetPublicStatusHistoryQuery, PublicStatusHistoryDto>
{
    private readonly StatusHistoryService _historyService;

    public GetPublicStatusHistoryQueryHandler(StatusHistoryService historyService)
    {
        _historyService = historyService;
    }

    public async Task<Option<PublicStatusHistoryDto>> RunQueryAsync(
        GetPublicStatusHistoryQuery query,
        CancellationToken cancellationToken
    )
    {
        var history = await _historyService
            .GetHistoryAsync(query.Days, cancellationToken)
            .ConfigureAwait(false);
        return Option.From(history);
    }
}
