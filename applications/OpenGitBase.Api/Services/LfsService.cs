using System.Text.RegularExpressions;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Git LFS on the control plane. The batch call decides access with the same rules as git and hands out one
/// signed action per object. Client transfers bypass the API: the dispatcher streams them between the client and
/// the storage nodes after <see cref="CheckTransferAsync"/>. <see cref="VerifyAsync"/> records an object once it
/// reached LFS Quorum: the primary and at least one encrypted replica hold it (<see cref="LfsReplicationService"/>).
/// </summary>
public sealed partial class LfsService
{
    private const string BasicTransfer = "basic";
    private const string Sha256 = "sha256";
    private const int RetryAfterSeconds = 10;
    private const string StorageUnavailable = "Repository storage is unavailable.";
    private const string MigrationMessage =
        "LFS requires encrypted replication; this repository is still migrating. Try again later.";
    private readonly LfsOptions _lfsOptions;
    private readonly GitOptions _gitOptions;
    private readonly RepositoryAccessCheckEvaluator _accessEvaluator;
    private readonly RepositoryStorageQuotaOptions _quotaOptions;
    private readonly IQueryProcessor _queryProcessor;
    private readonly LfsActionTokenService _tokens;
    private readonly IStorageLfsClient _storage;
    private readonly LfsReplicationService _replication;
    private readonly ILfsCopyCompletionScheduler _completions;
    private readonly ILfsRepairQueue _repairs;
    private readonly ILogger<LfsService> _logger;

    public LfsService(
        LfsOptions lfsOptions,
        GitOptions gitOptions,
        RepositoryAccessCheckEvaluator accessEvaluator,
        RepositoryStorageQuotaOptions quotaOptions,
        IQueryProcessor queryProcessor,
        LfsActionTokenService tokens,
        IStorageLfsClient storage,
        LfsReplicationService replication,
        ILfsCopyCompletionScheduler completions,
        ILfsRepairQueue repairs,
        ILogger<LfsService> logger
    )
    {
        _lfsOptions = lfsOptions;
        _gitOptions = gitOptions;
        _accessEvaluator = accessEvaluator;
        _quotaOptions = quotaOptions;
        _queryProcessor = queryProcessor;
        _tokens = tokens;
        _storage = storage;
        _replication = replication;
        _completions = completions;
        _repairs = repairs;
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

        var evaluation = await EvaluateAccessAsync(
                request,
                operation == LfsTransferOperation.Upload ? RepositoryOperation.WriteGit : RepositoryOperation.ReadGit,
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
            return await DeniedAsync(request, operation, access.Reason, requestId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (request.Objects.Count > _lfsOptions.MaxBatchObjects)
        {
            return Error(
                StatusCodes.Status413RequestEntityTooLarge,
                $"Too many objects in one batch: {request.Objects.Count} (maximum {_lfsOptions.MaxBatchObjects}).",
                requestId
            );
        }

        var validOids = request
            .Objects.Where(item => IsValidObject(item))
            .Select(item => item.Oid)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var recorded = await FindRecordedAsync(repositoryId, validOids, cancellationToken).ConfigureAwait(false);
        if (operation == LfsTransferOperation.Upload)
        {
            var quotaFailure = await CheckQuotaAsync(request, recorded, requestId, cancellationToken)
                .ConfigureAwait(false);
            if (quotaFailure is not null)
            {
                return quotaFailure;
            }

            var replicationFailure = await CheckEncryptedReplicationAsync(
                    request,
                    repositoryId,
                    recorded,
                    requestId,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (replicationFailure is not null)
            {
                return replicationFailure;
            }
        }

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
        var targets = await FindTransferTargetsAsync(claims.RepositoryId, operation, cancellationToken)
            .ConfigureAwait(false);
        if (targets.Count == 0)
        {
            return Unavailable(StorageUnavailable, requestId);
        }

        return new LfsResult(
            StatusCodes.Status200OK,
            new LfsTransferCheckResponse
            {
                RepositoryId = claims.RepositoryId,
                Oid = claims.Oid,
                Size = claims.Size,
                Targets = targets.Select(RepositoryAccessCheckRoutingMapper.MapRoutingTarget).ToList(),
            }
        );
    }

    /// <summary>
    /// A download found no storage target holding a recorded object. Queues an LFS repair of the repository
    /// (requests for a repository already waiting collapse into one) and answers 202. The dispatcher tells the
    /// client to retry.
    /// </summary>
    public async Task<LfsResult> RequestRepairAsync(LfsRepairRequest request, string requestId, CancellationToken cancellationToken)
    {
        if (!_lfsOptions.Enabled)
        {
            return Error(StatusCodes.Status501NotImplemented, "Git LFS is not enabled on this server.", requestId);
        }

        var authorization = await AuthorizeTransferAsync(
                request.Token,
                request.Oid,
                LfsTransferOperation.Download,
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
        _logger.LogWarning(
            "LFS object {Oid} of {RepositoryId} was on no download target; queued an LFS repair",
            claims.Oid,
            claims.RepositoryId
        );
        _repairs.Schedule(claims.RepositoryId);
        return new LfsResult(StatusCodes.Status202Accepted, new { });
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

        var alreadyRecorded = await FindRecordedAsync(claims.RepositoryId, [claims.Oid], cancellationToken)
            .ConfigureAwait(false);
        if (alreadyRecorded.ContainsKey(claims.Oid))
        {
            return new LfsResult(StatusCodes.Status200OK, new Dictionary<string, string>());
        }

        var replicas = await _replication
            .ResolveReplicaSetAsync(claims.RepositoryId, cancellationToken)
            .ConfigureAwait(false);
        if (replicas is null)
        {
            return Unavailable(StorageUnavailable, requestId);
        }

        long? storedSize;
        try
        {
            storedSize = await _storage
                .GetObjectSizeAsync(
                    replicas.Primary,
                    claims.RepositoryId,
                    claims.Oid,
                    LfsObjectCopyKind.Plaintext,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "LFS verify of {Oid} could not reach storage node {Node}",
                claims.Oid,
                replicas.Primary.InternalHost
            );
            return Unavailable(StorageUnavailable, requestId);
        }

        if (storedSize != claims.Size)
        {
            return Error(
                StatusCodes.Status422UnprocessableEntity,
                "The object was not uploaded completely; push it again.",
                requestId
            );
        }

        if (replicas.EncryptedReplicas.Count == 0)
        {
            return Unavailable(MigrationMessage, requestId);
        }

        var quorum = await _replication
            .ReachQuorumAsync(replicas, claims.Oid, claims.Size, cancellationToken)
            .ConfigureAwait(false);
        if (quorum.Status != LfsQuorumStatus.Reached)
        {
            return Unavailable(
                "The object could not be stored on an encrypted replica yet; the upload is retried.",
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
                    KeyVersion = quorum.KeyVersion,
                    CreatedByUserId = claims.UserId is { } userId ? UserId.From(userId) : null,
                    Copies = quorum.Copies,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (recorded.IsNone)
        {
            return Error(StatusCodes.Status404NotFound, "Repository not found.", requestId);
        }

        _completions.Schedule(quorum.Pending!);
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

    /// <summary>A 503 that tells git-lfs when to try again.</summary>
    private static LfsResult Unavailable(string message, string requestId) =>
        Error(StatusCodes.Status503ServiceUnavailable, message, requestId) with { RetryAfterSeconds = RetryAfterSeconds };

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
        var repository = await FindRepositoryAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        if (repository is null || repository.Id.Value != claims.RepositoryId)
        {
            return (
                null,
                Error(StatusCodes.Status403Forbidden, "The LFS action token does not cover this repository.", requestId)
            );
        }

        return (claims, null);
    }

    private Task<RepositoryAccessCheckEvaluation> EvaluateAccessAsync(
        LfsBatchRequest request,
        RepositoryOperation operation,
        CancellationToken cancellationToken
    ) =>
        _accessEvaluator.EvaluateAsync(
            new RepositoryAccessCheckRequest
            {
                AccessToken = request.AccessToken,
                RepositoryPath = request.RepositoryPath,
                Operation = operation,
            },
            cancellationToken
        );

    /// <summary>
    /// An upload refused for lack of access answers 404 like a clone would, unless the caller can read the
    /// repository: then its existence is no secret and 403 tells git-lfs (and the user) what is wrong.
    /// </summary>
    private async Task<LfsResult> DeniedAsync(
        LfsBatchRequest request,
        LfsTransferOperation operation,
        string? reason,
        string requestId,
        CancellationToken cancellationToken
    )
    {
        var denied = Denied(reason, requestId);
        if (operation != LfsTransferOperation.Upload || denied.StatusCode != StatusCodes.Status404NotFound)
        {
            return denied;
        }

        var read = await EvaluateAccessAsync(request, RepositoryOperation.ReadGit, cancellationToken)
            .ConfigureAwait(false);
        var canRead = read.Response is { } response
            && (response.Allowed || Denied(response.Reason, requestId).StatusCode == StatusCodes.Status503ServiceUnavailable);
        return canRead
            ? Error(StatusCodes.Status403Forbidden, "You do not have write access to this repository.", requestId)
            : denied;
    }

    /// <summary>
    /// Objects the repository does not hold yet (each oid once) must fit into what is left of its byte limit,
    /// the same limit git pushes are checked against.
    /// </summary>
    private async Task<LfsResult?> CheckQuotaAsync(
        LfsBatchRequest request,
        IReadOnlyDictionary<string, long> recorded,
        string requestId,
        CancellationToken cancellationToken
    )
    {
        if (!_quotaOptions.Enabled)
        {
            return null;
        }

        var pendingBytes = request
            .Objects.Where(item =>
                IsValidObject(item) && item.Size <= _lfsOptions.MaxObjectBytes && !recorded.ContainsKey(item.Oid)
            )
            .GroupBy(item => item.Oid, StringComparer.Ordinal)
            .Sum(group => group.Max(item => item.Size));
        if (pendingBytes == 0)
        {
            return null;
        }

        var repository = await FindRepositoryAsync(request.RepositoryPath, cancellationToken).ConfigureAwait(false);
        if (repository is null)
        {
            return Error(StatusCodes.Status404NotFound, "Repository not found.", requestId);
        }

        var limit = await _accessEvaluator
            .ResolveRepositoryBytesLimitAsync(repository, cancellationToken)
            .ConfigureAwait(false);
        if (repository.StorageBytesUsed + pendingBytes <= limit)
        {
            return null;
        }

        var left = Math.Max(0, limit - repository.StorageBytesUsed);
        return Error(
            StatusCodes.Status507InsufficientStorage,
            $"Repository storage quota exceeded: the upload needs {pendingBytes} bytes, but only {left} of {limit} bytes are left.",
            requestId
        );
    }

    private async Task<RepositoryDto?> FindRepositoryAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var pathParts = repositoryPath.Split('/');
        if (pathParts.Length != 2)
        {
            return null;
        }

        var repository = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryByOwnerSlugQuery { OwnerSlug = pathParts[0], Slug = pathParts[1] },
                cancellationToken
            )
            .ConfigureAwait(false);
        return repository.IsSome ? repository.Get() : null;
    }

    /// <summary>
    /// Uploads go to the write primary. Downloads read from the plaintext replicas first and fall back to the
    /// primary; the dispatcher tries the targets in order and moves on when one lacks the object.
    /// </summary>
    private async Task<IReadOnlyList<RepositoryRoutingTargetDto>> FindTransferTargetsAsync(
        Guid repositoryId,
        LfsTransferOperation operation,
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
            return [];
        }

        var healthy = routing.Get().Targets.Where(target => target.IsHealthy).ToList();
        var primary = healthy.Where(target => target.IsPrimary).Take(1).ToList();
        if (operation == LfsTransferOperation.Upload)
        {
            return primary;
        }

        return
        [
            .. healthy.Where(target =>
                !target.IsPrimary && primary.All(main => main.StorageNodeId != target.StorageNodeId)
            ),
            .. primary,
        ];
    }

    /// <summary>
    /// Nothing is uploaded unless it can reach LFS Quorum: objects still to upload need a healthy encrypted replica.
    /// </summary>
    private async Task<LfsResult?> CheckEncryptedReplicationAsync(
        LfsBatchRequest request,
        Guid repositoryId,
        IReadOnlyDictionary<string, long> recorded,
        string requestId,
        CancellationToken cancellationToken
    )
    {
        var needsUpload = request.Objects.Any(item =>
            IsValidObject(item) && item.Size <= _lfsOptions.MaxObjectBytes && !recorded.ContainsKey(item.Oid)
        );
        if (!needsUpload)
        {
            return null;
        }

        var replicas = await _replication.ResolveReplicaSetAsync(repositoryId, cancellationToken).ConfigureAwait(false);
        if (replicas is null)
        {
            return Unavailable(StorageUnavailable, requestId);
        }

        return replicas.EncryptedReplicas.Count == 0 ? Unavailable(MigrationMessage, requestId) : null;
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

        if (item.Size > _lfsOptions.MaxObjectBytes)
        {
            return ObjectError(
                item,
                StatusCodes.Status422UnprocessableEntity,
                $"Object size {item.Size} bytes exceeds the maximum LFS object size of {_lfsOptions.MaxObjectBytes} bytes."
            );
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
