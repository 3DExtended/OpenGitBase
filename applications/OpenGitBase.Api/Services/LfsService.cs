using System.Text.RegularExpressions;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Git LFS on the control plane. The batch call decides access with the same rules as git and hands out one
/// signed action per object. Object bytes never pass through the API: the dispatcher streams them between the
/// client and the storage nodes after <see cref="CheckTransferAsync"/>, and <see cref="VerifyAsync"/> records
/// an object once storage holds it.
/// </summary>
public sealed partial class LfsService
{
    private const string BasicTransfer = "basic";
    private const string Sha256 = "sha256";
    private readonly LfsOptions _lfsOptions;
    private readonly GitOptions _gitOptions;
    private readonly RepositoryAccessCheckEvaluator _accessEvaluator;
    private readonly IQueryProcessor _queryProcessor;
    private readonly LfsActionTokenService _tokens;
    private readonly IStorageLfsClient _storage;
    private readonly ILogger<LfsService> _logger;

    public LfsService(
        LfsOptions lfsOptions,
        GitOptions gitOptions,
        RepositoryAccessCheckEvaluator accessEvaluator,
        IQueryProcessor queryProcessor,
        LfsActionTokenService tokens,
        IStorageLfsClient storage,
        ILogger<LfsService> logger
    )
    {
        _lfsOptions = lfsOptions;
        _gitOptions = gitOptions;
        _accessEvaluator = accessEvaluator;
        _queryProcessor = queryProcessor;
        _tokens = tokens;
        _storage = storage;
        _logger = logger;
    }

    public async Task<LfsResult> BatchAsync(
        LfsBatchRequest request,
        string requestId,
        CancellationToken cancellationToken
    )
    {
        if (!_lfsOptions.Enabled)
        {
            return Error(StatusCodes.Status501NotImplemented, "Git LFS is not enabled on this server.", requestId);
        }

        if (ParseOperation(request.Operation) is not { } operation)
        {
            return Error(StatusCodes.Status422UnprocessableEntity, "Operation must be upload or download.", requestId);
        }

        if (request.Transfers is { Count: > 0 } transfers && !transfers.Contains(BasicTransfer, StringComparer.Ordinal))
        {
            return Error(
                StatusCodes.Status422UnprocessableEntity,
                "Only the basic transfer adapter is supported.",
                requestId
            );
        }

        if (!string.IsNullOrEmpty(request.HashAlgo) && !string.Equals(request.HashAlgo, Sha256, StringComparison.Ordinal))
        {
            return Error(StatusCodes.Status409Conflict, "Only the sha256 hash algorithm is supported.", requestId);
        }

        if (string.IsNullOrWhiteSpace(request.AccessToken))
        {
            return Error(StatusCodes.Status401Unauthorized, "Credentials needed.", requestId);
        }

        var evaluation = await _accessEvaluator
            .EvaluateAsync(
                new RepositoryAccessCheckRequest
                {
                    AccessToken = request.AccessToken,
                    RepositoryPath = request.RepositoryPath,
                    Operation = operation == LfsTransferOperation.Upload
                        ? RepositoryOperation.WriteGit
                        : RepositoryOperation.ReadGit,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (evaluation.ValidationErrors is not null)
        {
            return Error(StatusCodes.Status404NotFound, "Repository not found.", requestId);
        }

        var access = evaluation.Response!;
        if (!access.Allowed || access.RepositoryId is not { } repositoryId)
        {
            return Denied(access.Reason, requestId);
        }

        var validOids = request
            .Objects.Where(item => IsValidObject(item))
            .Select(item => item.Oid)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var recorded = await FindRecordedAsync(repositoryId, validOids, cancellationToken).ConfigureAwait(false);
        var baseUrl = ResolveBaseUrl(request);
        var objects = request
            .Objects.Select(item =>
                !IsValidObject(item)
                    ? ObjectError(item, StatusCodes.Status422UnprocessableEntity, "Invalid object oid or size.")
                    : operation == LfsTransferOperation.Upload
                        ? PlanUpload(item, recorded, repositoryId, access.ResolvedUserId, baseUrl)
                        : PlanDownload(item, recorded, repositoryId, access.ResolvedUserId, baseUrl)
            )
            .ToList();

        return new LfsResult(
            StatusCodes.Status200OK,
            new LfsBatchResponseBody { Transfer = BasicTransfer, Objects = objects, HashAlgo = Sha256 }
        );
    }

    public async Task<LfsResult> CheckTransferAsync(
        LfsTransferCheckRequest request,
        string requestId,
        CancellationToken cancellationToken
    )
    {
        if (!_lfsOptions.Enabled)
        {
            return Error(StatusCodes.Status501NotImplemented, "Git LFS is not enabled on this server.", requestId);
        }

        if (ParseOperation(request.Operation) is not { } operation)
        {
            return Error(StatusCodes.Status422UnprocessableEntity, "Operation must be upload or download.", requestId);
        }

        var authorization = await AuthorizeTransferAsync(
                request.Token,
                request.Oid,
                operation,
                request.RepositoryPath,
                requestId,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (authorization.Failure is not null)
        {
            return authorization.Failure;
        }

        var claims = authorization.Claims!;
        var primary = await FindHealthyPrimaryAsync(claims.RepositoryId, cancellationToken).ConfigureAwait(false);
        if (primary is null)
        {
            return Error(StatusCodes.Status503ServiceUnavailable, "Repository storage is unavailable.", requestId);
        }

        return new LfsResult(
            StatusCodes.Status200OK,
            new LfsTransferCheckResponse
            {
                RepositoryId = claims.RepositoryId,
                Oid = claims.Oid,
                Size = claims.Size,
                Targets = [RepositoryAccessCheckRoutingMapper.MapRoutingTarget(primary)],
            }
        );
    }

    public async Task<LfsResult> VerifyAsync(
        LfsVerifyRequest request,
        string requestId,
        CancellationToken cancellationToken
    )
    {
        if (!_lfsOptions.Enabled)
        {
            return Error(StatusCodes.Status501NotImplemented, "Git LFS is not enabled on this server.", requestId);
        }

        var authorization = await AuthorizeTransferAsync(
                request.Token,
                request.Oid,
                LfsTransferOperation.Upload,
                request.RepositoryPath,
                requestId,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (authorization.Failure is not null)
        {
            return authorization.Failure;
        }

        var claims = authorization.Claims!;
        if (request.Size != claims.Size)
        {
            return Error(StatusCodes.Status422UnprocessableEntity, "Object size does not match the upload.", requestId);
        }

        var primary = await FindHealthyPrimaryAsync(claims.RepositoryId, cancellationToken).ConfigureAwait(false);
        var node = primary is null ? null : await ResolveEndpointAsync(primary, cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return Error(StatusCodes.Status503ServiceUnavailable, "Repository storage is unavailable.", requestId);
        }

        long? storedSize;
        try
        {
            storedSize = await _storage
                .GetObjectSizeAsync(node, claims.RepositoryId, claims.Oid, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "LFS verify of {Oid} could not reach storage node {Node}", claims.Oid, node.InternalHost);
            return Error(StatusCodes.Status503ServiceUnavailable, "Repository storage is unavailable.", requestId);
        }

        if (storedSize != claims.Size)
        {
            return Error(
                StatusCodes.Status422UnprocessableEntity,
                "The object was not uploaded completely; push it again.",
                requestId
            );
        }

        var recorded = await _queryProcessor
            .RunQueryAsync(
                new RecordLfsObjectQuery
                {
                    RepositoryId = RepositoryId.From(claims.RepositoryId),
                    Oid = claims.Oid,
                    Size = claims.Size,
                    KeyVersion = 0,
                    CreatedByUserId = claims.UserId is { } userId ? UserId.From(userId) : null,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (recorded.IsNone)
        {
            return Error(StatusCodes.Status404NotFound, "Repository not found.", requestId);
        }

        return new LfsResult(StatusCodes.Status200OK, new Dictionary<string, string>());
    }

    private static LfsBatchAction Action(string href, LfsIssuedActionToken issued) =>
        new()
        {
            Href = href,
            Header = new Dictionary<string, string> { ["Authorization"] = $"Bearer {issued.Token}" },
            ExpiresIn = issued.ExpiresInSeconds,
            ExpiresAt = issued.ExpiresAt,
        };

    private static LfsBatchObjectResponse ObjectError(LfsBatchObjectRequest item, int code, string message) =>
        new()
        {
            Oid = item.Oid,
            Size = item.Size,
            Error = new LfsObjectError { Code = code, Message = message },
        };

    private static bool IsValidObject(LfsBatchObjectRequest item) =>
        item.Size >= 0 && OidPattern().IsMatch(item.Oid ?? string.Empty);

    private static LfsTransferOperation? ParseOperation(string operation) =>
        operation switch
        {
            "upload" => LfsTransferOperation.Upload,
            "download" => LfsTransferOperation.Download,
            _ => null,
        };

    /// <summary>
    /// Maps an access-check refusal to the LFS status. A missing repository and a repository the user may not
    /// see get the same 404, so the answer reveals nothing about private repositories.
    /// </summary>
    private static LfsResult Denied(string? reason, string requestId)
    {
        var text = reason ?? string.Empty;
        if (text.Contains("invalid", StringComparison.OrdinalIgnoreCase)
            || text.Contains("expired", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not registered", StringComparison.OrdinalIgnoreCase))
        {
            return Error(StatusCodes.Status401Unauthorized, "Credentials are invalid or expired.", requestId);
        }

        if (text.Contains("write access", StringComparison.OrdinalIgnoreCase))
        {
            return Error(StatusCodes.Status403Forbidden, "You do not have write access to this repository.", requestId);
        }

        if (text.Contains("quorum", StringComparison.OrdinalIgnoreCase)
            || text.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not assigned to storage", StringComparison.OrdinalIgnoreCase)
            || text.Contains("read targets", StringComparison.OrdinalIgnoreCase)
            || text.Contains("capacity", StringComparison.OrdinalIgnoreCase))
        {
            return Error(StatusCodes.Status503ServiceUnavailable, "Repository storage is unavailable.", requestId);
        }

        return Error(StatusCodes.Status404NotFound, "Repository not found.", requestId);
    }

    private static LfsResult Error(int statusCode, string message, string requestId) =>
        new(statusCode, new LfsErrorBody { Message = message, RequestId = requestId });

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex OidPattern();

    private async Task<(LfsActionTokenClaims? Claims, LfsResult? Failure)> AuthorizeTransferAsync(
        string token,
        string oid,
        LfsTransferOperation operation,
        string repositoryPath,
        string requestId,
        CancellationToken cancellationToken
    )
    {
        var validation = _tokens.Validate(token, oid, operation);
        if (!validation.IsValid)
        {
            return (null, Error(StatusCodes.Status401Unauthorized, validation.Error!, requestId));
        }

        var claims = validation.Claims!;
        var pathParts = repositoryPath.Split('/');
        var repository = pathParts.Length == 2
            ? await _queryProcessor
                .RunQueryAsync(
                    new GetRepositoryByOwnerSlugQuery { OwnerSlug = pathParts[0], Slug = pathParts[1] },
                    cancellationToken
                )
                .ConfigureAwait(false)
            : Option<RepositoryDto>.None;
        if (repository.IsNone || repository.Get().Id.Value != claims.RepositoryId)
        {
            return (
                null,
                Error(StatusCodes.Status403Forbidden, "The LFS action token does not cover this repository.", requestId)
            );
        }

        return (claims, null);
    }

    private async Task<RepositoryRoutingTargetDto?> FindHealthyPrimaryAsync(
        Guid repositoryId,
        CancellationToken cancellationToken
    )
    {
        var routing = await _queryProcessor
            .RunQueryAsync(
                new RepositoryReplicationRoutingQuery { RepositoryId = RepositoryId.From(repositoryId) },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (routing.IsNone)
        {
            return null;
        }

        var primary = routing.Get().Targets.FirstOrDefault(target => target.IsPrimary);
        return primary is { IsHealthy: true } ? primary : null;
    }

    private async Task<StorageNodeEndpoint?> ResolveEndpointAsync(
        RepositoryRoutingTargetDto target,
        CancellationToken cancellationToken
    )
    {
        var token = await _queryProcessor
            .RunQueryAsync(
                new GetStorageNodeApiTokenQuery { StorageNodeId = StorageNodeId.From(target.StorageNodeId) },
                cancellationToken
            )
            .ConfigureAwait(false);
        return token.IsNone
            ? null
            : new StorageNodeEndpoint(target.StorageNodeId, target.InternalHost, target.InternalHttpPort, token.Get());
    }

    private async Task<IReadOnlyDictionary<string, long>> FindRecordedAsync(
        Guid repositoryId,
        IReadOnlyCollection<string> oids,
        CancellationToken cancellationToken
    )
    {
        var recorded = await _queryProcessor
            .RunQueryAsync(
                new FindLfsObjectsQuery { RepositoryId = RepositoryId.From(repositoryId), Oids = oids },
                cancellationToken
            )
            .ConfigureAwait(false);
        return recorded.IsSome
            ? recorded.Get().ToDictionary(item => item.Oid, item => item.Size, StringComparer.Ordinal)
            : new Dictionary<string, long>();
    }

    private LfsBatchObjectResponse PlanUpload(
        LfsBatchObjectRequest item,
        IReadOnlyDictionary<string, long> recorded,
        Guid repositoryId,
        Guid? userId,
        string baseUrl
    )
    {
        if (recorded.TryGetValue(item.Oid, out var recordedSize))
        {
            return new LfsBatchObjectResponse { Oid = item.Oid, Size = recordedSize };
        }

        var issued = _tokens.Issue(repositoryId, item.Oid, item.Size, LfsTransferOperation.Upload, userId);
        return new LfsBatchObjectResponse
        {
            Oid = item.Oid,
            Size = item.Size,
            Authenticated = true,
            Actions = new LfsBatchActions
            {
                Upload = Action($"{baseUrl}/objects/{item.Oid}", issued),
                Verify = Action($"{baseUrl}/objects/{item.Oid}/verify", issued),
            },
        };
    }

    private LfsBatchObjectResponse PlanDownload(
        LfsBatchObjectRequest item,
        IReadOnlyDictionary<string, long> recorded,
        Guid repositoryId,
        Guid? userId,
        string baseUrl
    )
    {
        if (!recorded.TryGetValue(item.Oid, out var size))
        {
            return ObjectError(item, StatusCodes.Status404NotFound, "Object does not exist.");
        }

        var issued = _tokens.Issue(repositoryId, item.Oid, size, LfsTransferOperation.Download, userId);
        return new LfsBatchObjectResponse
        {
            Oid = item.Oid,
            Size = size,
            Authenticated = true,
            Actions = new LfsBatchActions { Download = Action($"{baseUrl}/objects/{item.Oid}", issued) },
        };
    }

    private string ResolveBaseUrl(LfsBatchRequest request)
    {
        if (!string.IsNullOrWhiteSpace(_lfsOptions.PublicBaseUrl))
        {
            return $"{_lfsOptions.PublicBaseUrl.TrimEnd('/')}/{request.RepositoryPath}.git/info/lfs";
        }

        if (
            Uri.TryCreate(request.LfsBaseUrl, UriKind.Absolute, out var clientUrl)
            && (clientUrl.Scheme == Uri.UriSchemeHttps || clientUrl.Scheme == Uri.UriSchemeHttp)
        )
        {
            return clientUrl.ToString().TrimEnd('/');
        }

        return $"{_gitOptions.PublicBaseUrl.TrimEnd('/')}/{request.RepositoryPath}.git/info/lfs";
    }
}
