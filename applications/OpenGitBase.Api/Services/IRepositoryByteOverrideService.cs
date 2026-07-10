using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Services;

public interface IRepositoryByteOverrideService
{
    Task<RepositoryByteOverrideEligibilityDto> EvaluateAsync(
        RepositoryEntity repository,
        CancellationToken cancellationToken
    );
}
