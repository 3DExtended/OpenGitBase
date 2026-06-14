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
    private readonly ILogger<SshAuthenticationController> _logger;

    public SshAuthenticationController(
        IQueryProcessor queryProcessor,
        ILogger<SshAuthenticationController> logger
    )
    {
        _queryProcessor = queryProcessor;
        _logger = logger;
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
            _logger.LogWarning(
                "SSH authentication denied: fingerprint query parameter is missing. StatusCode={StatusCode}",
                StatusCodes.Status404NotFound
            );
            return NotFound();
        }

        var result = await _queryProcessor.RunQueryAsync(
            new GetPublicGitSshKeyByFingerprintQuery { Fingerprint = fingerprint },
            cancellationToken
        );

        if (result.IsNone)
        {
            _logger.LogWarning(
                "SSH authentication denied: fingerprint {FingerprintPrefix} is not registered. StatusCode={StatusCode}",
                DescribeFingerprint(fingerprint),
                StatusCodes.Status404NotFound
            );
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

        _logger.LogInformation(
            "SSH authentication succeeded: fingerprint={FingerprintPrefix}, keyName={KeyName}, ownerUserId={OwnerUserId}. StatusCode={StatusCode}",
            DescribeFingerprint(normalizedFingerprint),
            key.Name,
            key.OwnerUserId?.Value,
            StatusCodes.Status200OK
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

    private static string DescribeFingerprint(string fingerprint) =>
        fingerprint.Length <= 20 ? fingerprint : $"{fingerprint[..20]}...";
}
