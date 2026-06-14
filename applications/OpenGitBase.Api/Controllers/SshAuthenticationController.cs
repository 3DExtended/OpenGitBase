using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.PublicGitSshKey.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/ssh-authentication")]
public sealed class SshAuthenticationController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;

    public SshAuthenticationController(IQueryProcessor queryProcessor)
    {
        _queryProcessor = queryProcessor;
    }

    [HttpGet("by-fingerprint")]
    [ProducesResponseType(typeof(SshAuthenticationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SshAuthenticationResponse>> GetByFingerprint(
        [FromQuery] string fingerprint,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return NotFound();
        }

        var result = await _queryProcessor.RunQueryAsync(
            new GetPublicGitSshKeyByFingerprintQuery { Fingerprint = fingerprint },
            cancellationToken
        );

        if (result.IsNone)
        {
            return NotFound();
        }

        var key = result.Get();
        var normalizedFingerprint = SshKeyFingerprintNormalizer
            .GetLookupCandidates(key.Fingerprint ?? fingerprint)
            .First(candidate =>
                candidate.StartsWith(
                    SshKeyFingerprintNormalizer.Sha256Prefix,
                    StringComparison.Ordinal
                )
            );

        return Ok(
            new SshAuthenticationResponse
            {
                Fingerprint = normalizedFingerprint,
                PublicSshKey = key.PublicSSHKey,
                AuthorizedKeysLine = SshAuthorizedKeysLineBuilder.Build(
                    normalizedFingerprint,
                    key.PublicSSHKey
                ),
            }
        );
    }
}
