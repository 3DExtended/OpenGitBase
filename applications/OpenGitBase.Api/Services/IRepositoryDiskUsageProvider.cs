using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

public interface IRepositoryDiskUsageProvider
{
    Task<long?> GetDiskUsageBytesAsync(
        RepositoryDto repository,
        CancellationToken cancellationToken
    );
}
