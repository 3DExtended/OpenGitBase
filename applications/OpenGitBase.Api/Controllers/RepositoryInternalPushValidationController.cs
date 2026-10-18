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

        if (result.Allowed && request.LfsPointers.Count > 0)
        {
            var missing = await FindMissingLfsObjectAsync(repositoryDto.Id, request.LfsPointers, cancellationToken)
                .ConfigureAwait(false);
            if (missing is not null)
            {
                return Ok(new RepositoryPushValidationResponse { Allowed = false, Reason = missing });
            }
        }

        return Ok(
            new RepositoryPushValidationResponse
            {
                Allowed = result.Allowed,
                Reason = result.Reason,
            }
        );
    }

    private static string BranchName(string refName) =>
        refName.StartsWith("refs/heads/", StringComparison.Ordinal) ? refName["refs/heads/".Length..]
        : string.IsNullOrEmpty(refName) ? "<branch>"
        : refName;

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

    /// <summary>
    /// A ref must never point at an LFS object the server does not hold: every pointer the push introduces needs a
    /// recorded object (one that reached LFS Quorum). Applies whether or not LFS is enabled.
    /// </summary>
    private async Task<string?> FindMissingLfsObjectAsync(
        RepositoryId repositoryId,
        IReadOnlyList<GitPushLfsPointerRequest> pointers,
        CancellationToken cancellationToken
    )
    {
        var oids = pointers.Select(pointer => pointer.Oid).Distinct(StringComparer.Ordinal).ToList();
        var recorded = await _queryProcessor
            .RunQueryAsync(new FindLfsObjectsQuery { RepositoryId = repositoryId, Oids = oids }, cancellationToken)
            .ConfigureAwait(false);
        var present = recorded.IsSome
            ? recorded.Get().Select(item => item.Oid).ToHashSet(StringComparer.Ordinal)
            : [];
        var missing = pointers
            .Where(pointer => !present.Contains(pointer.Oid))
            .DistinctBy(pointer => pointer.Oid, StringComparer.Ordinal)
            .ToList();
        if (missing.Count == 0)
        {
            return null;
        }

        var first = missing[0];
        var fix = $"Run: git lfs push --all origin {BranchName(first.RefName)}";
        return missing.Count == 1
            ? $"LFS object {first.Oid} ({first.Path}) is missing on the server. {fix}"
            : $"LFS object {first.Oid} ({first.Path}) and {missing.Count - 1} more are missing on the server. {fix}";
    }
}
