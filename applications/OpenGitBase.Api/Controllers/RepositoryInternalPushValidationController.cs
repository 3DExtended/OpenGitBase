using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/internal/repositories/push-validation")]
public sealed class RepositoryInternalPushValidationController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly GitPushEnforcementService _gitPushEnforcementService;

    public RepositoryInternalPushValidationController(
        IQueryProcessor queryProcessor,
        GitPushEnforcementService gitPushEnforcementService
    )
    {
        _queryProcessor = queryProcessor;
        _gitPushEnforcementService = gitPushEnforcementService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(RepositoryPushValidationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RepositoryPushValidationResponse>> ValidatePush(
        [FromBody] RepositoryPushValidationRequest request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.PhysicalPath))
        {
            return BadRequest(new { error = "PhysicalPath is required." });
        }

        var repositoryId = TryParseRepositoryId(request.PhysicalPath);
        if (repositoryId is null)
        {
            return BadRequest(new { error = "Invalid physicalPath." });
        }

        var repository = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryQuery { ModelId = repositoryId },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (repository.IsNone)
        {
            return NotFound(new { error = "Repository not found." });
        }

        var repositoryDto = repository.Get();
        GitPushEnforcementResult result;
        if (request.ValidatePushRulesOnly)
        {
            result = await _gitPushEnforcementService
                .EvaluatePushRulesOnlyAsync(
                    repositoryDto,
                    request.Commits,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        else
        {
            var role = ParseRole(request.EffectiveRole);
            var userId = request.ResolvedUserId.HasValue
                ? UserId.From(request.ResolvedUserId.Value)
                : UserId.From(Guid.Empty);
            var isRepositoryOwner = repositoryDto.OwnerUserId == userId;

            result = await _gitPushEnforcementService
                .EvaluateAsync(
                    repositoryDto,
                    userId,
                    role,
                    isRepositoryOwner,
                    request.IsPlatformMergeIdentity,
                    request.RefUpdates,
                    request.Commits,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        return Ok(
            new RepositoryPushValidationResponse
            {
                Allowed = result.Allowed,
                Reason = result.Reason,
            }
        );
    }

    private static RepositoryRole ParseRole(string? effectiveRole) =>
        effectiveRole switch
        {
            nameof(RepositoryRole.Owner) => RepositoryRole.Owner,
            nameof(RepositoryRole.Admin) => RepositoryRole.Admin,
            _ when Enum.TryParse<RepositoryRole>(effectiveRole, out var parsed) => parsed,
            _ => RepositoryRole.None,
        };

    private static RepositoryId? TryParseRepositoryId(string physicalPath)
    {
        var fileName = Path.GetFileName(physicalPath.TrimEnd('/'));
        if (!fileName.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var idText = fileName[..^4];
        return Guid.TryParse(idText, out var id) ? RepositoryId.From(id) : null;
    }
}
