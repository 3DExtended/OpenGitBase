using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("sensitive")]
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
        var hasPublicKey = !string.IsNullOrWhiteSpace(request.PublicKey);
        var hasAccessToken = !string.IsNullOrWhiteSpace(request.AccessToken);
        if (hasPublicKey == hasAccessToken)
        {
            const string credentialMessage =
                "Exactly one of PublicKey or AccessToken must be provided.";
            ModelState.AddModelError(nameof(request.PublicKey), credentialMessage);
            ModelState.AddModelError(nameof(request.AccessToken), credentialMessage);
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

        var username = pathParts[0];
        var repositorySlug = pathParts[1];

        Option<UserId> authenticatingUserId;
        string? accessTokenScope = null;

        if (hasPublicKey)
        {
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

            authenticatingUserId = await _queryProcessor.RunQueryAsync(
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
        }
        else
        {
            var tokenValidation = await _queryProcessor.RunQueryAsync(
                new ValidateGitAccessTokenQuery { Token = request.AccessToken },
                cancellationToken
            );
            if (tokenValidation.IsNone)
            {
                return OkWithLog(
                    request,
                    new RepositoryAccessCheckResponse
                    {
                        Allowed = false,
                        Reason = "Access token is invalid or expired.",
                    }
                );
            }

            accessTokenScope = tokenValidation.Get().Scope;
            authenticatingUserId = Option.From(tokenValidation.Get().UserId);
        }

        if (
            accessTokenScope is not null
            && request.Operation == RepositoryOperation.WriteGit
            && accessTokenScope == GitAccessTokenScopes.Read
        )
        {
            return OkWithLog(
                request,
                new RepositoryAccessCheckResponse
                {
                    Allowed = false,
                    ResolvedUserId = authenticatingUserId.Get().Value,
                    Reason = "Token does not allow write access.",
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
                await ApplyStorageRoutingAsync(
                    repositoryDto,
                    new RepositoryAccessCheckResponse
                    {
                        Allowed = true,
                        ResolvedUserId = resolvedUserId,
                        RepositoryId = repositoryDto.Id.Value,
                        EffectiveRole = "Owner",
                    },
                    request.Operation,
                    cancellationToken
                )
            );
        }

        if (
            string.Equals(
                repositoryDto.OwnerKind,
                "organization",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            var organizationAccess = await ResolveOrganizationRepositoryAccessAsync(
                repositoryDto,
                authenticatingUserId.Get(),
                request.Operation,
                resolvedUserId,
                cancellationToken
            );

            if (organizationAccess is not null)
            {
                if (!organizationAccess.Allowed)
                {
                    return OkWithLog(request, organizationAccess);
                }

                return OkWithLog(
                    request,
                    await ApplyStorageRoutingAsync(
                        repositoryDto,
                        organizationAccess,
                        request.Operation,
                        cancellationToken
                    )
                );
            }
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
                await ApplyStorageRoutingAsync(
                    repositoryDto,
                    new RepositoryAccessCheckResponse
                    {
                        Allowed = true,
                        ResolvedUserId = resolvedUserId,
                        RepositoryId = repositoryDto.Id.Value,
                        EffectiveRole = effectiveRole,
                    },
                    request.Operation,
                    cancellationToken
                )
            );
        }

        if (request.Operation == RepositoryOperation.WriteGit)
        {
            var writeResponse = new RepositoryAccessCheckResponse
            {
                Allowed = role >= RepositoryRole.Writer,
                ResolvedUserId = resolvedUserId,
                RepositoryId = repositoryDto.Id.Value,
                EffectiveRole = effectiveRole,
                Reason = role >= RepositoryRole.Writer ? null : "Insufficient write access.",
            };

            if (!writeResponse.Allowed)
            {
                return OkWithLog(request, writeResponse);
            }

            return OkWithLog(
                request,
                await ApplyStorageRoutingAsync(
                    repositoryDto,
                    writeResponse,
                    request.Operation,
                    cancellationToken
                )
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

    private async Task<RepositoryAccessCheckResponse?> ResolveOrganizationRepositoryAccessAsync(
        RepositoryDto repositoryDto,
        UserId authenticatingUserId,
        RepositoryOperation operation,
        Guid resolvedUserId,
        CancellationToken cancellationToken
    )
    {
        var organizationResult = await _queryProcessor
            .RunQueryAsync(
                new GetOrganizationQuery
                {
                    ModelId = OrganizationId.From(repositoryDto.OwnerUserId.Value),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (organizationResult.IsNone)
        {
            return null;
        }

        var organization = organizationResult.Get();
        var effectiveRole = ResolveOrganizationMemberRole(
            organization,
            authenticatingUserId,
            await _queryProcessor
                .RunQueryAsync(
                    new GetOrganizationMemberQuery
                    {
                        OrganizationId = organization.Id,
                        UserId = authenticatingUserId,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false)
        );

        if (effectiveRole is null)
        {
            return null;
        }

        if (effectiveRole >= RepositoryRole.Writer)
        {
            return new RepositoryAccessCheckResponse
            {
                Allowed = true,
                ResolvedUserId = resolvedUserId,
                RepositoryId = repositoryDto.Id.Value,
                EffectiveRole = "Owner",
            };
        }

        if (operation == RepositoryOperation.ReadGit)
        {
            return new RepositoryAccessCheckResponse
            {
                Allowed = true,
                ResolvedUserId = resolvedUserId,
                RepositoryId = repositoryDto.Id.Value,
                EffectiveRole = nameof(RepositoryRole.Reader),
            };
        }

        return null;
    }

    private RepositoryRole? ResolveOrganizationMemberRole(
        OrganizationDto organization,
        UserId authenticatingUserId,
        Option<OrganizationMemberDto> membership
    )
    {
        if (organization.OwnerUserId == authenticatingUserId.Value)
        {
            return RepositoryRole.Writer;
        }

        if (membership.IsNone)
        {
            return null;
        }

        return membership.Get().Role == OrganizationMemberRole.Owner
            ? RepositoryRole.Writer
            : RepositoryRole.Reader;
    }

    private async Task<RepositoryAccessCheckResponse> ApplyStorageRoutingAsync(
        RepositoryDto repositoryDto,
        RepositoryAccessCheckResponse response,
        RepositoryOperation operation,
        CancellationToken cancellationToken
    )
    {
        if (!response.Allowed)
        {
            return response;
        }

        if (repositoryDto.StorageNodeId is null)
        {
            return new RepositoryAccessCheckResponse
            {
                Allowed = false,
                ResolvedUserId = response.ResolvedUserId,
                RepositoryId = response.RepositoryId,
                EffectiveRole = response.EffectiveRole,
                Reason = "Repository is not assigned to storage.",
            };
        }

        var routing = await _queryProcessor
            .RunQueryAsync(
                new RepositoryReplicationRoutingQuery
                {
                    RepositoryId = repositoryDto.Id,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (routing.IsNone)
        {
            return new RepositoryAccessCheckResponse
            {
                Allowed = false,
                ResolvedUserId = response.ResolvedUserId,
                RepositoryId = response.RepositoryId,
                EffectiveRole = response.EffectiveRole,
                Reason = "Repository replication routing is unavailable.",
            };
        }

        var routingDto = routing.Get();
        var primary = routingDto.Targets.FirstOrDefault(target => target.IsPrimary);
        if (primary is null || !primary.IsHealthy)
        {
            return new RepositoryAccessCheckResponse
            {
                Allowed = false,
                ResolvedUserId = response.ResolvedUserId,
                RepositoryId = response.RepositoryId,
                EffectiveRole = response.EffectiveRole,
                Reason = "Primary storage node is unavailable.",
            };
        }

        if (operation == RepositoryOperation.WriteGit && !routingDto.WriteQuorumAvailable)
        {
            return new RepositoryAccessCheckResponse
            {
                Allowed = false,
                ResolvedUserId = response.ResolvedUserId,
                RepositoryId = response.RepositoryId,
                EffectiveRole = response.EffectiveRole,
                Reason = "Write quorum unavailable: fewer than two storage nodes are healthy.",
            };
        }

        var readTargets = routingDto
            .Targets.Where(target => target.IsInSync)
            .OrderByDescending(target => target.IsPrimary)
            .ThenBy(target => target.StorageNodeId)
            .Select(RepositoryAccessCheckRoutingMapper.MapRoutingTarget)
            .ToList();

        if (operation == RepositoryOperation.ReadGit && readTargets.Count == 0)
        {
            return new RepositoryAccessCheckResponse
            {
                Allowed = false,
                ResolvedUserId = response.ResolvedUserId,
                RepositoryId = response.RepositoryId,
                EffectiveRole = response.EffectiveRole,
                Reason = "No in-sync read targets are available.",
            };
        }

        var selectedTarget = operation == RepositoryOperation.WriteGit
            ? RepositoryAccessCheckRoutingMapper.MapRoutingTarget(primary)
            : readTargets[0];

        return new RepositoryAccessCheckResponse
        {
            Allowed = true,
            ResolvedUserId = response.ResolvedUserId,
            RepositoryId = response.RepositoryId,
            EffectiveRole = response.EffectiveRole,
            PhysicalPath = repositoryDto.PhysicalPath,
            StorageNodeInternalHost = selectedTarget.InternalHost,
            StorageNodeInternalSshPort = selectedTarget.InternalSshPort,
            StorageNodeInternalGitHttpPort = selectedTarget.InternalGitHttpPort,
            ReplicationEpoch = routingDto.ReplicationEpoch,
            Primary = RepositoryAccessCheckRoutingMapper.MapRoutingTarget(primary),
            ReadTargets = readTargets,
        };
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
