using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/access-checks/repositories")]
public sealed class RepositoryAccessChecksController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly ISshKeyService _sshKeyService;

    public RepositoryAccessChecksController(
        IQueryProcessor queryProcessor,
        ISshKeyService sshKeyService
    )
    {
        _queryProcessor = queryProcessor;
        _sshKeyService = sshKeyService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(RepositoryAccessCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RepositoryAccessCheckResponse>> CheckRepositoryAccess(
        [FromBody] RepositoryAccessCheckRequest request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.PublicKey))
        {
            ModelState.AddModelError(nameof(request.PublicKey), "PublicKey is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RepositoryPath))
        {
            ModelState.AddModelError(nameof(request.RepositoryPath), "RepositoryPath is required.");
        }

        if (request.Operation == RepositoryOperation.Unknown)
        {
            ModelState.AddModelError(nameof(request.Operation), "Operation is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var pathParts = request.RepositoryPath.Split('/', StringSplitOptions.None);
        if (pathParts.Length != 2)
        {
            ModelState.AddModelError(
                nameof(request.RepositoryPath),
                "RepositoryPath must be in the format '{username}/{repositorySlug}'."
            );
        }
        else
        {
            if (string.IsNullOrWhiteSpace(pathParts[0]))
            {
                ModelState.AddModelError(
                    nameof(request.RepositoryPath),
                    "RepositoryPath username is required."
                );
            }

            if (string.IsNullOrWhiteSpace(pathParts[1]))
            {
                ModelState.AddModelError(
                    nameof(request.RepositoryPath),
                    "RepositoryPath repository slug is required."
                );
            }
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        string fingerprint;
        try
        {
            fingerprint =
                _sshKeyService.ValidateAndGetFingerprint(request.PublicKey)
                ?? throw new ArgumentException("Invalid SSH public key.");
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(nameof(request.PublicKey), ex.Message);
            return ValidationProblem(ModelState);
        }

        var username = pathParts[0];
        var repositorySlug = pathParts[1];

        var authenticatingUserId = await _queryProcessor.RunQueryAsync(
            new GetUserIdBySshKeyFingerprintQuery { Fingerprint = fingerprint },
            cancellationToken
        );
        if (authenticatingUserId.IsNone)
        {
            return Ok(
                new RepositoryAccessCheckResponse
                {
                    Allowed = false,
                    Reason = "SSH public key is not registered.",
                }
            );
        }

        var ownerUserId = await _queryProcessor.RunQueryAsync(
            new UserExistsByUsernameQuery { Username = username },
            cancellationToken
        );
        if (ownerUserId.IsNone)
        {
            return Ok(
                new RepositoryAccessCheckResponse
                {
                    Allowed = false,
                    ResolvedUserId = authenticatingUserId.Get().Value,
                    Reason = "User not found.",
                }
            );
        }

        var repository = await _queryProcessor.RunQueryAsync(
            new GetRepositoryBySlugForUserQuery
            {
                OwnerUserId = ownerUserId.Get(),
                Slug = repositorySlug,
            },
            cancellationToken
        );
        if (repository.IsNone)
        {
            return Ok(
                new RepositoryAccessCheckResponse
                {
                    Allowed = false,
                    ResolvedUserId = authenticatingUserId.Get().Value,
                    Reason = "Repository not found.",
                }
            );
        }

        var resolvedUserId = authenticatingUserId.Get().Value;
        var repositoryDto = repository.Get();

        if (repositoryDto.OwnerUserId == authenticatingUserId.Get())
        {
            return Ok(
                new RepositoryAccessCheckResponse
                {
                    Allowed = true,
                    ResolvedUserId = resolvedUserId,
                    RepositoryId = repositoryDto.Id.Value,
                    EffectiveRole = "Owner",
                }
            );
        }

        var repositoryMember = await _queryProcessor.RunQueryAsync(
            new GetRepositoryMemberQuery
            {
                RepositoryId = repositoryDto.Id,
                UserId = authenticatingUserId.Get(),
            },
            cancellationToken
        );

        if (repositoryMember.IsNone || repositoryMember.Get().Role == RepositoryRole.None)
        {
            return Ok(
                new RepositoryAccessCheckResponse
                {
                    Allowed = false,
                    ResolvedUserId = resolvedUserId,
                    RepositoryId = repositoryDto.Id.Value,
                    Reason = "No access to repository for user.",
                }
            );
        }

        var role = repositoryMember.Get().Role;
        var effectiveRole = role.ToString();

        if (request.Operation == RepositoryOperation.ReadGit)
        {
            return Ok(
                new RepositoryAccessCheckResponse
                {
                    Allowed = true,
                    ResolvedUserId = resolvedUserId,
                    RepositoryId = repositoryDto.Id.Value,
                    EffectiveRole = effectiveRole,
                }
            );
        }

        if (request.Operation == RepositoryOperation.WriteGit)
        {
            return Ok(
                new RepositoryAccessCheckResponse
                {
                    Allowed = role >= RepositoryRole.Writer,
                    ResolvedUserId = resolvedUserId,
                    RepositoryId = repositoryDto.Id.Value,
                    EffectiveRole = effectiveRole,
                }
            );
        }

        return Ok(
            new RepositoryAccessCheckResponse
            {
                Allowed = false,
                ResolvedUserId = resolvedUserId,
                RepositoryId = repositoryDto.Id.Value,
                EffectiveRole = effectiveRole,
                Reason = "Unknown operation.",
            }
        );
    }
}
