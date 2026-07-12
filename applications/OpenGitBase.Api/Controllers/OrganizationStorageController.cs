using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Authorize]
[Route("organization/{organizationId:guid}/storage")]
public sealed class OrganizationStorageController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;
    private readonly IOrganizationAccessService _organizationAccess;
    private readonly RepositoryStorageQuotaOptions _quotaOptions;

    public OrganizationStorageController(
        IQueryProcessor queryProcessor,
        IUserContext userContext,
        IOrganizationAccessService organizationAccess,
        RepositoryStorageQuotaOptions quotaOptions
    )
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
        _organizationAccess = organizationAccess;
        _quotaOptions = quotaOptions;
    }

    [HttpGet("settings")]
    public async Task<ActionResult<OrganizationStorageSettingsDto>> GetSettings(
        Guid organizationId,
        CancellationToken cancellationToken
    )
    {
        var access = await AuthorizeOwnerAsync(organizationId, cancellationToken)
            .ConfigureAwait(false);
        if (access is not null)
        {
            return access;
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new GetOrganizationStorageSettingsQuery
                {
                    OrganizationId = OrganizationId.From(organizationId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return NotFound();
        }

        return Ok(await EnrichSettingsAsync(result.Get(), cancellationToken).ConfigureAwait(false));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<OrganizationStorageSettingsDto>> UpdateSettings(
        Guid organizationId,
        [FromBody] UpdateOrganizationStorageSettingsRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await AuthorizeOwnerAsync(organizationId, cancellationToken)
            .ConfigureAwait(false);
        if (access is not null)
        {
            return access;
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UpdateOrganizationStorageSettingsQuery
                {
                    OrganizationId = OrganizationId.From(organizationId),
                    DefaultPlacementPolicy = request.DefaultPlacementPolicy,
                    DefaultSelfHostPreference = request.DefaultSelfHostPreference,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return NotFound();
        }

        return Ok(await EnrichSettingsAsync(result.Get(), cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("nodes")]
    public async Task<ActionResult<IReadOnlyList<StorageNodeDto>>> ListNodes(
        Guid organizationId,
        CancellationToken cancellationToken
    )
    {
        var access = await AuthorizeOwnerAsync(organizationId, cancellationToken)
            .ConfigureAwait(false);
        if (access is not null)
        {
            return access;
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ListOrganizationStorageNodesQuery { OrganizationId = organizationId },
                cancellationToken
            )
            .ConfigureAwait(false);

        return Ok(result.IsSome ? result.Get() : Array.Empty<StorageNodeDto>());
    }

    [HttpGet("enrollments")]
    public async Task<ActionResult<IReadOnlyList<StorageNodeEnrollmentDto>>> ListEnrollments(
        Guid organizationId,
        CancellationToken cancellationToken
    )
    {
        var access = await AuthorizeOwnerAsync(organizationId, cancellationToken)
            .ConfigureAwait(false);
        if (access is not null)
        {
            return access;
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ListStorageNodeEnrollmentsQuery { OrganizationId = organizationId },
                cancellationToken
            )
            .ConfigureAwait(false);

        return Ok(result.IsSome ? result.Get() : Array.Empty<StorageNodeEnrollmentDto>());
    }

    [HttpPost("enrollments")]
    public async Task<ActionResult<CreateStorageNodeEnrollmentResult>> CreateEnrollment(
        Guid organizationId,
        [FromBody] CreateOrganizationStorageEnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await AuthorizeOwnerAsync(organizationId, cancellationToken)
            .ConfigureAwait(false);
        if (access is not null)
        {
            return access;
        }

        if (string.IsNullOrWhiteSpace(request.NodeId))
        {
            return BadRequest(new { error = "NodeId is required." });
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new CreateStorageNodeEnrollmentQuery
                {
                    NodeId = request.NodeId,
                    CreatedByUserId = _userContext.User.UserId,
                    ExpiresInHours = request.ExpiresInHours,
                    OrganizationId = organizationId,
                    MaxBytes = request.MaxBytes,
                    HostingScope = request.HostingScope,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return BadRequest(new { error = "Could not create enrollment." });
        }

        return Ok(result.Get());
    }

    [HttpPatch("nodes/{storageNodeId:guid}/hosting-scope")]
    public async Task<ActionResult<StorageNodeDto>> UpdateHostingScope(
        Guid organizationId,
        Guid storageNodeId,
        [FromBody] UpdateStorageNodeHostingScopeRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await AuthorizeOwnerAsync(organizationId, cancellationToken)
            .ConfigureAwait(false);
        if (access is not null)
        {
            return access;
        }

        var nodes = await _queryProcessor
            .RunQueryAsync(
                new ListOrganizationStorageNodesQuery { OrganizationId = organizationId },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (
            nodes.IsNone
            || !nodes.Get().Any(node => node.Id.Value == storageNodeId)
        )
        {
            return NotFound();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UpdateStorageNodeHostingScopeQuery
                {
                    StorageNodeId = StorageNodeId.From(storageNodeId),
                    HostingScope = request.HostingScope,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome ? Ok(result.Get()) : NotFound();
    }

    [HttpPatch("nodes/{storageNodeId:guid}/capacity")]
    public async Task<ActionResult<StorageNodeDto>> UpdateCapacity(
        Guid organizationId,
        Guid storageNodeId,
        [FromBody] UpdateStorageNodeCapacityRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await AuthorizeOwnerAsync(organizationId, cancellationToken)
            .ConfigureAwait(false);
        if (access is not null)
        {
            return access;
        }

        var nodes = await _queryProcessor
            .RunQueryAsync(
                new ListOrganizationStorageNodesQuery { OrganizationId = organizationId },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (
            nodes.IsNone
            || !nodes.Get().Any(node => node.Id.Value == storageNodeId)
        )
        {
            return NotFound();
        }

        if (request.MaxBytes < 0)
        {
            return BadRequest(new { error = "MaxBytes must be non-negative." });
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UpdateStorageNodeCapacityQuery
                {
                    StorageNodeId = StorageNodeId.From(storageNodeId),
                    MaxBytes = request.MaxBytes,
                    EnforceUsedBytesFloor = true,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return BadRequest(new { error = "MaxBytes cannot be less than current used bytes on this node." });
        }

        return Ok(result.Get());
    }

    private async Task<ActionResult?> AuthorizeOwnerAsync(
        Guid organizationId,
        CancellationToken cancellationToken
    )
    {
        var access = await _organizationAccess
            .CheckOwnerAccessAsync(
                OrganizationId.From(organizationId),
                _userContext.GetUserId(),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!access.OrganizationExists)
        {
            return NotFound();
        }

        if (!access.IsOwner)
        {
            return Forbid();
        }

        return null;
    }

    private async Task<OrganizationStorageSettingsDto> EnrichSettingsAsync(
        OrganizationStorageSettingsDto settings,
        CancellationToken cancellationToken
    )
    {
        var quotaResult = await _queryProcessor
            .RunQueryAsync(
                new GetOrganizationStorageQuotaQuery
                {
                    OrganizationId = OrganizationId.From(settings.OrganizationId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (quotaResult.IsSome)
        {
            var quota = quotaResult.Get();
            settings.PlatformBytesLimit = quota.PlatformBytesLimit;
            settings.ContributedBytesCapacity = quota.ContributedBytesCapacity;
            settings.BytesLimit = quota.BytesLimit;
        }
        else
        {
            settings.PlatformBytesLimit = _quotaOptions.MaxBytes;
            settings.BytesLimit = _quotaOptions.MaxBytes;
        }

        return settings;
    }
}
