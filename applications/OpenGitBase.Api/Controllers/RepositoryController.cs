using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Route("repository")]
[Authorize]
public class RepositoryController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;
    private readonly IOrganizationAccessService _organizationAccess;
    private readonly RepositoryStorageQuotaOptions _quotaOptions;
    private readonly IRepositoryDiskUsageProvider _repositoryDiskUsageProvider;
    private readonly RepositoryContentAuthorizationService _authorization;
    private readonly RepositoryResponseMapper _responseMapper;

    public RepositoryController(
        IQueryProcessor queryProcessor,
        IUserContext userContext,
        IOrganizationAccessService organizationAccess,
        RepositoryStorageQuotaOptions quotaOptions,
        IRepositoryDiskUsageProvider repositoryDiskUsageProvider,
        RepositoryContentAuthorizationService authorization,
        RepositoryResponseMapper responseMapper
    )
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
        _organizationAccess = organizationAccess;
        _quotaOptions = quotaOptions;
        _repositoryDiskUsageProvider = repositoryDiskUsageProvider;
        _authorization = authorization;
        _responseMapper = responseMapper;
    }

    [HttpPost("{slug}")]
    public async Task<IActionResult> Create(
        [FromRoute] string slug,
        [FromBody] CreateRepositoryRequest request,
        CancellationToken cancellationToken
    )
    {
        var user = await _queryProcessor
            .RunQueryAsync(
                new UserGetByIdQuery { ModelId = UserId.From(_userContext.User.UserId) },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (user.IsNone)
        {
            return Unauthorized(new { error = "User not found." });
        }

        var emailVerified = await _queryProcessor
            .RunQueryAsync(
                new UserGetEmailVerifiedQuery { UserId = UserId.From(_userContext.User.UserId) },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (emailVerified.IsNone || !emailVerified.Get())
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { error = "Email must be verified before creating repositories." }
            );
        }

        if (string.IsNullOrWhiteSpace(slug) || slug.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
        {
            return BadRequest(
                new
                {
                    error = "Invalid slug. Slug must be non-empty and can only contain letters, digits and hyphens.",
                }
            );
        }

        if (ReservedSlugValidator.IsReserved(slug))
        {
            return Conflict(new { error = "Reserved repository slug." });
        }

        var currentUserId = UserId.From(_userContext.User.UserId);
        var ownerUserId = currentUserId;

        if (!string.IsNullOrWhiteSpace(request.OrganizationSlug))
        {
            var organization = await _queryProcessor
                .RunQueryAsync(
                    new GetOrganizationBySlugQuery { Slug = request.OrganizationSlug.Trim() },
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (organization.IsNone)
            {
                return NotFound(new { error = "Organization not found." });
            }

            var access = await _organizationAccess
                .CheckMemberAccessAsync(organization.Get().Id, currentUserId, cancellationToken)
                .ConfigureAwait(false);

            if (!access.OrganizationExists)
            {
                return NotFound(new { error = "Organization not found." });
            }

            if (!access.IsMember)
            {
                return Forbid();
            }

            ownerUserId = UserId.From(organization.Get().Id.Value);
        }

        var repository = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryBySlugForUserQuery { Slug = slug, OwnerUserId = ownerUserId },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (repository.IsSome)
        {
            return BadRequest(new { error = "Repository with this slug already exists." });
        }

        var query = new CreateRepositoryWithStorageQuery
        {
            ModelToCreate = new RepositoryDto
            {
                Slug = slug,
                OwnerUserId = ownerUserId,
                Name = request.RepositoryName,
                IsPrivate = request.IsPrivate,
            },
        };

        var result = await _queryProcessor.RunQueryAsync(query, cancellationToken);
        if (result.IsNone)
        {
            return NotFound();
        }

        var payload = result.Get();
        if (payload.RepositoryId is null)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = payload.Error ?? "Repository creation failed." }
            );
        }

        var id = payload.RepositoryId;
        return CreatedAtAction(nameof(Get), new { id = id.Value }, id);
    }

    [AllowAnonymous]
    [HttpGet("by-slug/{owner}/{slug}")]
    public async Task<IActionResult> GetByOwnerSlug(
        string owner,
        string slug,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeReadAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);
        if (access.Kind != RepositoryContentAccessResultKind.Allowed || access.Repository is null)
        {
            return MapAccessFailure(access);
        }

        return Ok(_responseMapper.MapRepository(access.Repository));
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/usage")]
    public async Task<IActionResult> GetUsage(Guid id, CancellationToken cancellationToken)
    {
        var repositoryId = RepositoryId.From(id);
        var access = await _authorization
            .AuthorizeReadByIdAsync(repositoryId, cancellationToken)
            .ConfigureAwait(false);
        if (access.Kind != RepositoryContentAccessResultKind.Allowed || access.Repository is null)
        {
            return MapAccessFailure(access);
        }

        var result = await _queryProcessor.RunQueryAsync(
            new GetRepositoryUsageQuery { RepositoryId = repositoryId },
            cancellationToken
        );
        if (result.IsNone)
        {
            return NotFound();
        }

        var usage = result.Get();
        var liveBytes = await _repositoryDiskUsageProvider
            .GetDiskUsageBytesAsync(access.Repository, cancellationToken)
            .ConfigureAwait(false);
        if (liveBytes.HasValue)
        {
            usage = new RepositoryUsageDto
            {
                BytesUsed = liveBytes.Value,
                BytesLimit = usage.BytesLimit,
                FileSizeLimit = usage.FileSizeLimit,
            };
        }

        return Ok(usage);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var access = await _authorization
            .AuthorizeReadByIdAsync(RepositoryId.From(id), cancellationToken)
            .ConfigureAwait(false);
        if (access.Kind != RepositoryContentAccessResultKind.Allowed || access.Repository is null)
        {
            return MapAccessFailure(access);
        }

        return Ok(_responseMapper.MapRepository(access.Repository));
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _queryProcessor.RunQueryAsync(
            new ListRepositoriesForUserQuery { UserId = UserId.From(_userContext.User.UserId) },
            cancellationToken
        );
        if (result.IsNone)
        {
            return NotFound();
        }

        return Ok(_responseMapper.MapRepositories(result.Get()));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateMetadata(
        Guid id,
        [FromBody] UpdateRepositoryRequest request,
        CancellationToken cancellationToken
    )
    {
        var getResult = await _queryProcessor.RunQueryAsync(
            new GetRepositoryQuery { ModelId = RepositoryId.From(id) },
            cancellationToken
        );

        if (getResult.IsNone)
        {
            return NotFound();
        }

        if (getResult.Get().OwnerUserId != UserId.From(_userContext.User.UserId))
        {
            return Forbid();
        }

        var existing = getResult.Get();
        var query = new UpdateRepositoryQuery
        {
            UpdatedModel = new RepositoryDto
            {
                Id = RepositoryId.From(id),
                Name = request.Name,
                IsPrivate = request.IsPrivate,
                Slug = existing.Slug,
                OwnerUserId = existing.OwnerUserId,
                PhysicalPath = existing.PhysicalPath,
                StorageBytesUsed = existing.StorageBytesUsed,
            },
        };
        var result = await _queryProcessor.RunQueryAsync(query, cancellationToken);
        if (result.IsNone)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPatch("{id:guid}/placement-policy")]
    public async Task<IActionResult> UpdatePlacementPolicy(
        Guid id,
        [FromBody] UpdateRepositoryPlacementPolicyRequest request,
        CancellationToken cancellationToken
    )
    {
        var getResult = await _queryProcessor.RunQueryAsync(
            new GetRepositoryQuery { ModelId = RepositoryId.From(id) },
            cancellationToken
        );

        if (getResult.IsNone)
        {
            return NotFound();
        }

        var repository = getResult.Get();
        if (!await CanManageRepositoryAsync(repository, cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _queryProcessor.RunQueryAsync(
            new UpdateRepositoryPlacementPolicyQuery
            {
                RepositoryId = RepositoryId.From(id),
                PlacementPolicy = request.PlacementPolicy,
            },
            cancellationToken
        );

        return result.IsSome ? Ok(result.Get()) : NotFound();
    }

    [HttpGet("{id:guid}/byte-override-eligibility")]
    public async Task<IActionResult> GetByteOverrideEligibility(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var getResult = await _queryProcessor.RunQueryAsync(
            new GetRepositoryQuery { ModelId = RepositoryId.From(id) },
            cancellationToken
        );

        if (getResult.IsNone)
        {
            return NotFound();
        }

        if (!await CanManageRepositoryAsync(getResult.Get(), cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _queryProcessor.RunQueryAsync(
            new GetRepositoryByteOverrideEligibilityQuery { RepositoryId = RepositoryId.From(id) },
            cancellationToken
        );

        return result.IsSome ? Ok(result.Get()) : NotFound();
    }

    [HttpPatch("{id:guid}/max-bytes-override")]
    public async Task<IActionResult> UpdateMaxBytesOverride(
        Guid id,
        [FromBody] UpdateRepositoryMaxBytesOverrideRequest request,
        CancellationToken cancellationToken
    )
    {
        var getResult = await _queryProcessor.RunQueryAsync(
            new GetRepositoryQuery { ModelId = RepositoryId.From(id) },
            cancellationToken
        );

        if (getResult.IsNone)
        {
            return NotFound();
        }

        if (!await CanManageRepositoryAsync(getResult.Get(), cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _queryProcessor.RunQueryAsync(
            new UpdateRepositoryMaxBytesOverrideQuery
            {
                RepositoryId = RepositoryId.From(id),
                MaxBytesOverride = request.MaxBytesOverride,
            },
            cancellationToken
        );

        return result.IsSome ? Ok(result.Get()) : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var getResult = await _queryProcessor.RunQueryAsync(
            new GetRepositoryQuery { ModelId = RepositoryId.From(id) },
            cancellationToken
        );

        if (getResult.IsNone)
        {
            return NotFound();
        }

        if (getResult.Get().OwnerUserId != UserId.From(_userContext.User.UserId))
        {
            return Forbid();
        }

        var result = await _queryProcessor.RunQueryAsync(
            new DeleteRepositoryWithStorageQuery { Id = RepositoryId.From(id) },
            cancellationToken
        );

        if (result.IsNone)
        {
            return NotFound();
        }

        var payload = result.Get();
        if (!payload.Success)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = payload.Error ?? "Repository deletion failed." }
            );
        }

        return NoContent();
    }

    private async Task<bool> CanManageRepositoryAsync(
        RepositoryDto repository,
        CancellationToken cancellationToken
    )
    {
        var userId = _userContext.GetUserId();
        if (repository.OwnerUserId == userId)
        {
            return true;
        }

        var organizationAccess = await _organizationAccess
            .CheckOwnerAccessAsync(
                OrganizationId.From(repository.OwnerUserId.Value),
                userId,
                cancellationToken
            )
            .ConfigureAwait(false);

        return organizationAccess.OrganizationExists && organizationAccess.IsOwner;
    }

    private IActionResult MapAccessFailure(RepositoryContentAccessResult access) =>
        access.Kind switch
        {
            RepositoryContentAccessResultKind.NotFound => NotFound(),
            RepositoryContentAccessResultKind.Forbidden => Forbid(),
            RepositoryContentAccessResultKind.Unavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable
            ),
            _ => NotFound(),
        };
}
