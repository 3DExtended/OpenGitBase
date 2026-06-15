using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Options;
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
    private readonly ILogger<RepositoryAccessChecksController> _logger;
    private readonly RepositoryStorageQuotaOptions _quotaOptions;

    public RepositoryAccessChecksController(
        IQueryProcessor queryProcessor,
        ISshKeyService sshKeyService,
        ILogger<RepositoryAccessChecksController> logger,
        RepositoryStorageQuotaOptions quotaOptions
    )
    {
        _queryProcessor = queryProcessor;
        _sshKeyService = sshKeyService;
        _logger = logger;
        _quotaOptions = quotaOptions;
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
            return LogValidationFailureAndReturn(request);
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
            return LogValidationFailureAndReturn(request);
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
            return LogValidationFailureAndReturn(request);
        }

        var username = pathParts[0];
        var repositorySlug = pathParts[1];

        var authenticatingUserId = await _queryProcessor.RunQueryAsync(
            new GetUserIdBySshKeyFingerprintQuery { Fingerprint = fingerprint },
            cancellationToken
        );
        if (authenticatingUserId.IsNone)
        {
            return OkWithLog(
                request,
                new RepositoryAccessCheckResponse
                {
                    Allowed = false,
                    Reason = "SSH public key is not registered.",
                }
            );
        }

        var repository = await _queryProcessor.RunQueryAsync(
            new GetRepositoryByOwnerSlugQuery { OwnerSlug = username, Slug = repositorySlug },
            cancellationToken
        );
        if (repository.IsNone)
        {
            var ownerUserId = await _queryProcessor.RunQueryAsync(
                new UserExistsByUsernameQuery { Username = username },
                cancellationToken
            );
            return OkWithLog(
                request,
                new RepositoryAccessCheckResponse
                {
                    Allowed = false,
                    ResolvedUserId = authenticatingUserId.IsSome
                        ? authenticatingUserId.Get().Value
                        : null,
                    Reason = ownerUserId.IsNone
                        ? "User of repo path not found."
                        : "Repository of repo path not found.",
                }
            );
        }

        var resolvedUserId = authenticatingUserId.Get().Value;
        var repositoryDto = repository.Get();

        if (request.Operation == RepositoryOperation.WriteGit && _quotaOptions.Enabled)
        {
            if (request.MaxFileBytes > 0 && request.MaxFileBytes > _quotaOptions.MaxFileBytes)
            {
                return OkWithLog(
                    request,
                    new RepositoryAccessCheckResponse
                    {
                        Allowed = false,
                        ResolvedUserId = resolvedUserId,
                        RepositoryId = repositoryDto.Id.Value,
                        Reason = "File exceeds maximum allowed size.",
                    }
                );
            }

            if (
                request.PackSizeBytes > 0
                && repositoryDto.StorageBytesUsed + request.PackSizeBytes > _quotaOptions.MaxBytes
            )
            {
                return OkWithLog(
                    request,
                    new RepositoryAccessCheckResponse
                    {
                        Allowed = false,
                        ResolvedUserId = resolvedUserId,
                        RepositoryId = repositoryDto.Id.Value,
                        Reason = "Repository storage quota exceeded.",
                    }
                );
            }
        }

        if (repositoryDto.OwnerUserId == authenticatingUserId.Get())
        {
            return OkWithLog(
                request,
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
            return OkWithLog(
                request,
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
            return OkWithLog(
                request,
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
            return OkWithLog(
                request,
                new RepositoryAccessCheckResponse
                {
                    Allowed = role >= RepositoryRole.Writer,
                    ResolvedUserId = resolvedUserId,
                    RepositoryId = repositoryDto.Id.Value,
                    EffectiveRole = effectiveRole,
                    Reason = role >= RepositoryRole.Writer ? null : "Insufficient write access.",
                }
            );
        }

        return OkWithLog(
            request,
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

    private ActionResult<RepositoryAccessCheckResponse> LogValidationFailureAndReturn(
        RepositoryAccessCheckRequest request
    )
    {
        var errors = string.Join(
            "; ",
            ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .SelectMany(entry =>
                    entry.Value!.Errors.Select(error => $"{entry.Key}: {error.ErrorMessage}")
                )
        );

        _logger.LogWarning(
            "Repository access check rejected: validation failed ({Errors}). Operation={Operation}, RepositoryPath={RepositoryPath}. StatusCode={StatusCode}",
            errors,
            request.Operation,
            request.RepositoryPath,
            StatusCodes.Status400BadRequest
        );

        return ValidationProblem(ModelState);
    }

    private ActionResult<RepositoryAccessCheckResponse> OkWithLog(
        RepositoryAccessCheckRequest request,
        RepositoryAccessCheckResponse response
    )
    {
        if (response.Allowed)
        {
            _logger.LogInformation(
                "Repository access allowed: operation={Operation}, path={RepositoryPath}, role={Role}, userId={UserId}, repositoryId={RepositoryId}. StatusCode={StatusCode}",
                request.Operation,
                request.RepositoryPath,
                response.EffectiveRole,
                response.ResolvedUserId,
                response.RepositoryId,
                StatusCodes.Status200OK
            );
        }
        else
        {
            _logger.LogWarning(
                "Repository access denied: reason={Reason}, operation={Operation}, path={RepositoryPath}, userId={UserId}, repositoryId={RepositoryId}, role={Role}. StatusCode={StatusCode}",
                response.Reason,
                request.Operation,
                request.RepositoryPath,
                response.ResolvedUserId,
                response.RepositoryId,
                response.EffectiveRole,
                StatusCodes.Status200OK
            );
        }

        return Ok(response);
    }
}
