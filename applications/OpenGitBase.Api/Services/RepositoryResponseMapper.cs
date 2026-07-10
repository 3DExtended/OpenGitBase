using OpenGitBase.Api.Models;
using OpenGitBase.Common.Security;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class RepositoryResponseMapper
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RepositoryResponseMapper(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public object MapRepository(RepositoryDto repository) =>
        IsInternalCaller() ? repository : RepositorySummaryResponse.From(repository);

    public IReadOnlyList<object> MapRepositories(IReadOnlyList<RepositoryDto> repositories) =>
        repositories.Select(MapRepository).ToList();

    private bool IsInternalCaller()
    {
        var remoteIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
        return remoteIp is not null && InternalNetworkAddress.IsInternal(remoteIp);
    }
}
