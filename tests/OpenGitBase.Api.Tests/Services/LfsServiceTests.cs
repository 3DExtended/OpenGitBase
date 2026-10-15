using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Api.Tests.Services.Lfs;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Tests.Services;

public class LfsServiceTests
{
    private const string RecordedOid = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string NewOid = "2222222222222222222222222222222222222222222222222222222222222222";
    private const string BaseUrl = "https://git.example.com/alice/repo.git/info/lfs";
    private const string RequestId = "req-1";
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly IQueryProcessor _queryProcessor = Substitute.For<IQueryProcessor>();
    private readonly FakeLfsStorageNodes _nodes = new();
    private readonly IRepositoryKeyService _keys = Substitute.For<IRepositoryKeyService>();
    private readonly ILfsCopyCompletionScheduler _completions = Substitute.For<ILfsCopyCompletionScheduler>();
    private readonly RecordingLfsRepairQueue _repairs = new();
    private readonly ISystemClock _clock = Substitute.For<ISystemClock>();
    private readonly UserId _ownerUserId = UserId.From(Guid.NewGuid());
    private readonly RepositoryId _repositoryId = RepositoryId.From(Guid.NewGuid());
    private readonly Guid _primaryNodeId = Guid.NewGuid();
    private readonly LfsOptions _lfsOptions = new() { Enabled = true, ActionTokenLifetimeSeconds = 3600 };
    private readonly RepositoryStorageQuotaOptions _quotaOptions = new() { Enabled = true, MaxBytes = 1_073_741_824 };
    private readonly RepositoryDto _repository;
    private readonly byte[] _content = RandomNumberGenerator.GetBytes(5000);
    private readonly string _contentOid;
    private bool _primaryHealthy = true;
    private bool _readReplicaHealthy = true;
    private bool _encryptedReplicasHealthy = true;

    public LfsServiceTests()
    {
        _repository = new RepositoryDto
        {
            Id = _repositoryId,
            OwnerUserId = _ownerUserId,
            Slug = "repo",
            Name = "Repo",
            PhysicalPath = $"/srv/git/{_repositoryId.Value}.git",
            StorageNodeId = StorageNodeId.From(_primaryNodeId),
        };
        _contentOid = Convert.ToHexStringLower(SHA256.HashData(_content));
        _clock.UtcNow.Returns(_ => Now);
        _keys
            .TryGetRepositoryKeyAsync(_repositoryId.Value, Arg.Any<CancellationToken>())
            .Returns(_ => new EphemeralRepositoryKey(RandomNumberGenerator.GetBytes(32), 1));
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryReplicationContextQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
                Option.From(
                    new RepositoryReplicationContextDto
                    {
                        RepositoryId = _repositoryId.Value,
                        IsPrimary = true,
                        Peers =
                        [
                            Peer("storage-1", "Primary", _primaryHealthy),
                            Peer("storage-4", "ReadReplica", _readReplicaHealthy),
                            Peer("storage-2", "EncryptedReplica", _encryptedReplicasHealthy),
                            Peer("storage-3", "EncryptedReplica", _encryptedReplicasHealthy),
                        ],
                    }
                )
            );
        ConfigureOwnerWithToken(GitAccessTokenScopes.Write);
        _queryProcessor
            .RunQueryAsync(Arg.Any<FindLfsObjectsQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var query = callInfo.Arg<FindLfsObjectsQuery>();
                IReadOnlyList<LfsObjectDto> recorded = query.Oids.Contains(RecordedOid)
                    ? [new LfsObjectDto { Oid = RecordedOid, Size = 42 }]
                    : [];
                return Option.From(recorded);
            });
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeApiTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From("node-token"));
        _queryProcessor
            .RunQueryAsync(Arg.Any<RecordLfsObjectQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(new RecordLfsObjectResult { Created = true }));
    }

    [Fact]
    public async Task BatchAsync_WhenLfsIsDisabled_Answers501()
    {
        _lfsOptions.Enabled = false;

        var result = await CreateService().BatchAsync(DownloadBatch(RecordedOid), RequestId, CancellationToken.None);

        Assert.Equal(501, result.StatusCode);
        var error = Assert.IsType<LfsErrorBody>(result.Body);
        Assert.Equal(RequestId, error.RequestId);
    }

    [Fact]
    public async Task BatchAsync_WithoutCredentials_Answers401()
    {
        var request = DownloadBatch(RecordedOid);
        request.AccessToken = string.Empty;

        var result = await CreateService().BatchAsync(request, RequestId, CancellationToken.None);

        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task BatchAsync_WithAnInvalidAccessToken_Answers401()
    {
        _queryProcessor
            .RunQueryAsync(Arg.Any<ValidateGitAccessTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<ValidateGitAccessTokenResult>.None);

        var result = await CreateService().BatchAsync(DownloadBatch(RecordedOid), RequestId, CancellationToken.None);

        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task BatchAsync_Download_OffersRecordedObjectsWithASignedDownloadAction()
    {
        var service = CreateService();

        var result = await service.BatchAsync(DownloadBatch(RecordedOid), RequestId, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        var body = Assert.IsType<LfsBatchResponseBody>(result.Body);
        Assert.Equal("basic", body.Transfer);
        Assert.Equal("sha256", body.HashAlgo);
        var item = Assert.Single(body.Objects);
        Assert.Equal(RecordedOid, item.Oid);
        Assert.Equal(42, item.Size);
        Assert.True(item.Authenticated);
        var download = Assert.IsType<LfsBatchAction>(item.Actions?.Download);
        Assert.Equal($"{BaseUrl}/objects/{RecordedOid}", download.Href);
        Assert.Equal(3600, download.ExpiresIn);
        var token = BearerToken(download);
        var claims = CreateTokenService().Validate(token, RecordedOid, LfsTransferOperation.Download).Claims;
        Assert.NotNull(claims);
        Assert.Equal(_repositoryId.Value, claims.RepositoryId);
        Assert.Equal(42, claims.Size);
    }

    [Fact]
    public async Task BatchAsync_Download_ReportsUnrecordedObjectsAsMissing()
    {
        var result = await CreateService().BatchAsync(DownloadBatch(NewOid), RequestId, CancellationToken.None);

        var body = Assert.IsType<LfsBatchResponseBody>(result.Body);
        var item = Assert.Single(body.Objects);
        Assert.Null(item.Actions);
        Assert.Equal(404, item.Error?.Code);
    }

    [Fact]
    public async Task BatchAsync_Upload_OffersUploadAndVerifyOnlyForObjectsNotYetRecorded()
    {
        var result = await CreateService().BatchAsync(UploadBatch(RecordedOid, NewOid), RequestId, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        var body = Assert.IsType<LfsBatchResponseBody>(result.Body);
        var recorded = Assert.Single(body.Objects, item => item.Oid == RecordedOid);
        Assert.Null(recorded.Actions);
        var upload = Assert.Single(body.Objects, item => item.Oid == NewOid);
        var uploadAction = Assert.IsType<LfsBatchAction>(upload.Actions?.Upload);
        var verifyAction = Assert.IsType<LfsBatchAction>(upload.Actions?.Verify);
        Assert.Equal($"{BaseUrl}/objects/{NewOid}", uploadAction.Href);
        Assert.Equal($"{BaseUrl}/objects/{NewOid}/verify", verifyAction.Href);
        var claims = CreateTokenService()
            .Validate(BearerToken(uploadAction), NewOid, LfsTransferOperation.Upload)
            .Claims;
        Assert.NotNull(claims);
        Assert.Equal(5_000_000, claims.Size);
        Assert.Equal(_ownerUserId.Value, claims.UserId);
    }

    [Fact]
    public async Task BatchAsync_Upload_WithAReadOnlyToken_Answers403()
    {
        ConfigureOwnerWithToken(GitAccessTokenScopes.Read);

        var result = await CreateService().BatchAsync(UploadBatch(NewOid), RequestId, CancellationToken.None);

        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task BatchAsync_UsesTheConfiguredPublicBaseUrlForHrefs()
    {
        _lfsOptions.PublicBaseUrl = "https://lfs.example.org/";

        var result = await CreateService().BatchAsync(DownloadBatch(RecordedOid), RequestId, CancellationToken.None);

        var body = Assert.IsType<LfsBatchResponseBody>(result.Body);
        Assert.Equal(
            $"https://lfs.example.org/alice/repo.git/info/lfs/objects/{RecordedOid}",
            body.Objects[0].Actions?.Download?.Href
        );
    }

    [Fact]
    public async Task BatchAsync_WithAnInvalidOid_ReportsAPerObjectError()
    {
        var result = await CreateService().BatchAsync(UploadBatch("../../etc/passwd"), RequestId, CancellationToken.None);

        var body = Assert.IsType<LfsBatchResponseBody>(result.Body);
        Assert.Equal(422, Assert.Single(body.Objects).Error?.Code);
    }

    [Fact]
    public async Task BatchAsync_WhenTheClientOffersNoBasicTransfer_Answers422()
    {
        var request = DownloadBatch(RecordedOid);
        request.Transfers = ["tus"];

        var result = await CreateService().BatchAsync(request, RequestId, CancellationToken.None);

        Assert.Equal(422, result.StatusCode);
    }

    [Fact]
    public async Task BatchAsync_WhenTheClientAsksForAnotherHashAlgorithm_Answers409()
    {
        var request = DownloadBatch(RecordedOid);
        request.HashAlgo = "sha512";

        var result = await CreateService().BatchAsync(request, RequestId, CancellationToken.None);

        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task BatchAsync_Upload_ByAReadOnlyMember_Answers403()
    {
        ConfigureMember(RepositoryRole.Reader);

        var result = await CreateService().BatchAsync(UploadBatch(NewOid), RequestId, CancellationToken.None);

        Assert.Equal(403, result.StatusCode);
        Assert.Contains("write access", Assert.IsType<LfsErrorBody>(result.Body).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BatchAsync_Download_ByAReadOnlyMember_OffersTheObject()
    {
        ConfigureMember(RepositoryRole.Reader);

        var result = await CreateService().BatchAsync(DownloadBatch(RecordedOid), RequestId, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        var body = Assert.IsType<LfsBatchResponseBody>(result.Body);
        Assert.NotNull(Assert.Single(body.Objects).Actions?.Download);
    }

    [Fact]
    public async Task BatchAsync_Upload_ByAnOrganizationMemberWithoutWriteAccess_Answers403()
    {
        var member = ConfigureOrganizationRepository(OrganizationMemberRole.Member);
        ConfigureCaller(member);

        var result = await CreateService().BatchAsync(UploadBatch(NewOid), RequestId, CancellationToken.None);

        Assert.Equal(403, result.StatusCode);
    }

    [Theory]
    [InlineData("upload")]
    [InlineData("download")]
    public async Task BatchAsync_ByANonMemberOfAPrivateRepository_AnswersLikeAMissingRepository(string operation)
    {
        _repository.IsPrivate = true;
        ConfigureMember(RepositoryRole.None);
        var request = operation == "upload" ? UploadBatch(NewOid) : DownloadBatch(RecordedOid);

        var result = await CreateService().BatchAsync(request, RequestId, CancellationToken.None);

        _queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);
        var missing = await CreateService().BatchAsync(request, RequestId, CancellationToken.None);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(missing.StatusCode, result.StatusCode);
        Assert.Equal(
            Assert.IsType<LfsErrorBody>(missing.Body).Message,
            Assert.IsType<LfsErrorBody>(result.Body).Message
        );
    }

    [Fact]
    public async Task BatchAsync_Upload_ObjectAboveTheMaximumObjectSize_ReportsAPerObject422WithTheLimit()
    {
        _lfsOptions.MaxObjectBytes = 4_000_000;

        var result = await CreateService().BatchAsync(UploadBatch(NewOid), RequestId, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        var item = Assert.Single(Assert.IsType<LfsBatchResponseBody>(result.Body).Objects);
        Assert.Null(item.Actions);
        Assert.Equal(422, item.Error?.Code);
        Assert.Contains("4000000 bytes", item.Error?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BatchAsync_WithMoreObjectsThanTheBatchLimit_Answers413()
    {
        _lfsOptions.MaxBatchObjects = 2;
        var oids = Enumerable.Range(1, 3).Select(i => i.ToString("x64")).ToArray();

        var result = await CreateService().BatchAsync(DownloadBatch(oids), RequestId, CancellationToken.None);

        Assert.Equal(413, result.StatusCode);
        Assert.Contains("2", Assert.IsType<LfsErrorBody>(result.Body).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BatchAsync_Upload_WhenNewObjectsExceedTheRepositoryQuota_Answers507WithTheLimit()
    {
        _repository.MaxBytesOverride = 4_000_000;

        var result = await CreateService().BatchAsync(UploadBatch(NewOid), RequestId, CancellationToken.None);

        Assert.Equal(507, result.StatusCode);
        Assert.Contains("4000000 bytes", Assert.IsType<LfsErrorBody>(result.Body).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BatchAsync_Upload_CountsEachMissingObjectOnceAgainstTheQuota()
    {
        _repository.StorageBytesUsed = 500_000;
        _repository.MaxBytesOverride = 6_000_000;

        var result = await CreateService()
            .BatchAsync(UploadBatch(RecordedOid, NewOid, NewOid), RequestId, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task BatchAsync_Upload_UsesTheOrganizationQuotaWhenTheRepositoryHasNoOverride()
    {
        ConfigureOrganizationRepository(OrganizationMemberRole.Owner);
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationStorageQuotaQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(new OrganizationStorageQuotaDto { BytesLimit = 4_000_000 }));

        var result = await CreateService().BatchAsync(UploadBatch(NewOid), RequestId, CancellationToken.None);

        Assert.Equal(507, result.StatusCode);
    }

    [Fact]
    public async Task BatchAsync_Upload_WhenQuotasAreDisabled_IgnoresTheLimit()
    {
        _quotaOptions.Enabled = false;
        _repository.MaxBytesOverride = 1;

        var result = await CreateService().BatchAsync(UploadBatch(NewOid), RequestId, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task BatchAsync_Download_IsAllowedWhenTheRepositoryIsOverQuota()
    {
        _repository.StorageBytesUsed = 10_000_000;
        _repository.MaxBytesOverride = 1_000;

        var result = await CreateService().BatchAsync(DownloadBatch(RecordedOid), RequestId, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task RequestRepairAsync_ForAValidDownloadToken_QueuesAnLfsRepairOfTheRepository()
    {
        var token = IssueToken(RecordedOid, 42, LfsTransferOperation.Download);

        var result = await CreateService().RequestRepairAsync(Repair(token, RecordedOid), RequestId, CancellationToken.None);

        Assert.Equal(202, result.StatusCode);
        Assert.Equal([_repositoryId.Value], _repairs.Scheduled);
    }

    [Fact]
    public async Task RequestRepairAsync_WithAnUploadToken_Answers401AndQueuesNothing()
    {
        var token = IssueToken(RecordedOid, 42, LfsTransferOperation.Upload);

        var result = await CreateService().RequestRepairAsync(Repair(token, RecordedOid), RequestId, CancellationToken.None);

        Assert.Equal(401, result.StatusCode);
        Assert.Empty(_repairs.Scheduled);
    }

    [Fact]
    public async Task RequestRepairAsync_WithATokenOfAnotherRepository_Answers403AndQueuesNothing()
    {
        var token = CreateTokenService()
            .Issue(Guid.NewGuid(), RecordedOid, 42, LfsTransferOperation.Download, _ownerUserId.Value)
            .Token;

        var result = await CreateService().RequestRepairAsync(Repair(token, RecordedOid), RequestId, CancellationToken.None);

        Assert.Equal(403, result.StatusCode);
        Assert.Empty(_repairs.Scheduled);
    }

    [Fact]
    public async Task CheckTransferAsync_ForAValidUploadToken_ReturnsThePrimaryAndTheDeclaredSize()
    {
        var token = IssueToken(NewOid, 5_000_000, LfsTransferOperation.Upload);

        var result = await CreateService().CheckTransferAsync(
            TransferCheck(token, NewOid, "upload"),
            RequestId,
            CancellationToken.None
        );

        Assert.Equal(200, result.StatusCode);
        var body = Assert.IsType<LfsTransferCheckResponse>(result.Body);
        Assert.Equal(_repositoryId.Value, body.RepositoryId);
        Assert.Equal(5_000_000, body.Size);
        var target = Assert.Single(body.Targets);
        Assert.Equal("storage-1", target.InternalHost);
        Assert.Equal(8082, target.InternalGitHttpPort);
    }

    [Fact]
    public async Task CheckTransferAsync_WhenTokenBelongsToAnotherRepository_Answers403()
    {
        var token = CreateTokenService()
            .Issue(Guid.NewGuid(), NewOid, 1, LfsTransferOperation.Upload, _ownerUserId.Value)
            .Token;

        var result = await CreateService().CheckTransferAsync(
            TransferCheck(token, NewOid, "upload"),
            RequestId,
            CancellationToken.None
        );

        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task CheckTransferAsync_WhenTokenExpired_Answers401()
    {
        var token = IssueToken(NewOid, 1, LfsTransferOperation.Download);
        _clock.UtcNow.Returns(Now.AddHours(2));

        var result = await CreateService().CheckTransferAsync(
            TransferCheck(token, NewOid, "download"),
            RequestId,
            CancellationToken.None
        );

        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task CheckTransferAsync_WhenTokenWasIssuedForAnotherOperation_Answers401()
    {
        var token = IssueToken(NewOid, 1, LfsTransferOperation.Download);

        var result = await CreateService().CheckTransferAsync(
            TransferCheck(token, NewOid, "upload"),
            RequestId,
            CancellationToken.None
        );

        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task CheckTransferAsync_WhenThePrimaryIsDown_Answers503()
    {
        _primaryHealthy = false;
        ConfigureOwnerWithToken(GitAccessTokenScopes.Write);
        var token = IssueToken(NewOid, 1, LfsTransferOperation.Upload);

        var result = await CreateService().CheckTransferAsync(
            TransferCheck(token, NewOid, "upload"),
            RequestId,
            CancellationToken.None
        );

        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public async Task CheckTransferAsync_Download_OffersTheReadReplicaBeforeThePrimary()
    {
        var token = IssueToken(RecordedOid, 42, LfsTransferOperation.Download);

        var result = await CreateService().CheckTransferAsync(
            TransferCheck(token, RecordedOid, "download"),
            RequestId,
            CancellationToken.None
        );

        var body = Assert.IsType<LfsTransferCheckResponse>(result.Body);
        Assert.Equal(["storage-4", "storage-1"], body.Targets.Select(target => target.InternalHost));
    }

    [Fact]
    public async Task CheckTransferAsync_Download_WhenTheReadReplicaIsDown_OffersThePrimary()
    {
        _readReplicaHealthy = false;
        var token = IssueToken(RecordedOid, 42, LfsTransferOperation.Download);

        var result = await CreateService().CheckTransferAsync(
            TransferCheck(token, RecordedOid, "download"),
            RequestId,
            CancellationToken.None
        );

        var body = Assert.IsType<LfsTransferCheckResponse>(result.Body);
        Assert.Equal(["storage-1"], body.Targets.Select(target => target.InternalHost));
    }

    [Fact]
    public async Task BatchAsync_Upload_WithoutAHealthyEncryptedReplica_Answers503WithTheMigrationMessage()
    {
        _encryptedReplicasHealthy = false;

        var result = await CreateService().BatchAsync(UploadBatch(NewOid), RequestId, CancellationToken.None);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal(
            "LFS requires encrypted replication; this repository is still migrating. Try again later.",
            Assert.IsType<LfsErrorBody>(result.Body).Message
        );
        Assert.NotNull(result.RetryAfterSeconds);
    }

    [Fact]
    public async Task BatchAsync_Upload_WhenEveryObjectIsRecorded_NeedsNoEncryptedReplica()
    {
        _encryptedReplicasHealthy = false;

        var result = await CreateService().BatchAsync(UploadBatch(RecordedOid), RequestId, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task VerifyAsync_WhenThePrimaryAndAnEncryptedReplicaHoldTheObject_RecordsItWithTheQuorumCopies()
    {
        _nodes.Store("storage-1", _repositoryId.Value, _contentOid, LfsObjectCopyKind.Plaintext, _content);
        _nodes.FailingHosts.Add("storage-3");
        var token = IssueToken(_contentOid, _content.Length, LfsTransferOperation.Upload);

        var result = await CreateService().VerifyAsync(
            Verify(token, _contentOid, _content.Length),
            RequestId,
            CancellationToken.None
        );

        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(_nodes.Find("storage-2", _repositoryId.Value, _contentOid, LfsObjectCopyKind.Encrypted));
        await _queryProcessor
            .Received(1)
            .RunQueryAsync(
                Arg.Is<RecordLfsObjectQuery>(query =>
                    query.RepositoryId == _repositoryId
                    && query.Oid == _contentOid
                    && query.Size == _content.Length
                    && query.KeyVersion == 1
                    && query.CreatedByUserId == _ownerUserId
                    && query.Copies.Count == 2
                    && query.Copies.Any(copy =>
                        copy.StorageNodeId == _primaryNodeId && copy.Kind == LfsObjectCopyKind.Plaintext
                    )
                    && query.Copies.Any(copy =>
                        copy.StorageNodeId == FakeLfsStorageNodes.NodeId("storage-2")
                        && copy.Kind == LfsObjectCopyKind.Encrypted
                    )
                ),
                Arg.Any<CancellationToken>()
            );
        _completions.Received(1).Schedule(Arg.Is<LfsPendingCopies>(pending => pending.Oid == _contentOid));
    }

    [Fact]
    public async Task VerifyAsync_WhenNoEncryptedReplicaConfirms_Answers503WithRetryAfterAndRecordsNothing()
    {
        _nodes.Store("storage-1", _repositoryId.Value, _contentOid, LfsObjectCopyKind.Plaintext, _content);
        _nodes.FailingHosts.Add("storage-2");
        _nodes.FailingHosts.Add("storage-3");
        var token = IssueToken(_contentOid, _content.Length, LfsTransferOperation.Upload);

        var result = await CreateService().VerifyAsync(
            Verify(token, _contentOid, _content.Length),
            RequestId,
            CancellationToken.None
        );

        Assert.Equal(503, result.StatusCode);
        Assert.NotNull(result.RetryAfterSeconds);
        await _queryProcessor
            .DidNotReceive()
            .RunQueryAsync(Arg.Any<RecordLfsObjectQuery>(), Arg.Any<CancellationToken>());
        _completions.DidNotReceive().Schedule(Arg.Any<LfsPendingCopies>());
    }

    [Fact]
    public async Task VerifyAsync_WithoutAHealthyEncryptedReplica_Answers503WithTheMigrationMessage()
    {
        _encryptedReplicasHealthy = false;
        _nodes.Store("storage-1", _repositoryId.Value, _contentOid, LfsObjectCopyKind.Plaintext, _content);
        var token = IssueToken(_contentOid, _content.Length, LfsTransferOperation.Upload);

        var result = await CreateService().VerifyAsync(
            Verify(token, _contentOid, _content.Length),
            RequestId,
            CancellationToken.None
        );

        Assert.Equal(503, result.StatusCode);
        Assert.Contains("still migrating", Assert.IsType<LfsErrorBody>(result.Body).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheObjectIsAlreadyRecorded_Answers200WithoutTouchingStorage()
    {
        var token = IssueToken(RecordedOid, 42, LfsTransferOperation.Upload);

        var result = await CreateService().VerifyAsync(Verify(token, RecordedOid, 42), RequestId, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        Assert.Empty(_nodes.Requests);
        await _queryProcessor
            .DidNotReceive()
            .RunQueryAsync(Arg.Any<RecordLfsObjectQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyAsync_WhenThePrimaryIsDown_Answers503WithRetryAfter()
    {
        _primaryHealthy = false;
        var token = IssueToken(NewOid, 5_000_000, LfsTransferOperation.Upload);

        var result = await CreateService().VerifyAsync(Verify(token, NewOid, 5_000_000), RequestId, CancellationToken.None);

        Assert.Equal(503, result.StatusCode);
        Assert.NotNull(result.RetryAfterSeconds);
    }

    [Fact]
    public async Task VerifyAsync_WhenThePrimaryLacksTheObject_Answers422AndRecordsNothing()
    {
        var token = IssueToken(NewOid, 5_000_000, LfsTransferOperation.Upload);

        var result = await CreateService().VerifyAsync(Verify(token, NewOid, 5_000_000), RequestId, CancellationToken.None);

        Assert.Equal(422, result.StatusCode);
        await _queryProcessor
            .DidNotReceive()
            .RunQueryAsync(Arg.Any<RecordLfsObjectQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyAsync_WhenTheSizeDiffersFromTheUploadToken_Answers422()
    {
        var token = IssueToken(NewOid, 5_000_000, LfsTransferOperation.Upload);

        var result = await CreateService().VerifyAsync(Verify(token, NewOid, 4_000_000), RequestId, CancellationToken.None);

        Assert.Equal(422, result.StatusCode);
    }

    [Fact]
    public async Task VerifyAsync_WithADownloadToken_Answers401()
    {
        var token = IssueToken(NewOid, 5_000_000, LfsTransferOperation.Download);

        var result = await CreateService().VerifyAsync(Verify(token, NewOid, 5_000_000), RequestId, CancellationToken.None);

        Assert.Equal(401, result.StatusCode);
    }

    private static string BearerToken(LfsBatchAction action)
    {
        var header = action.Header["Authorization"];
        Assert.StartsWith("Bearer ", header);
        return header["Bearer ".Length..];
    }

    private static LfsBatchRequest DownloadBatch(params string[] oids) =>
        new()
        {
            AccessToken = "ogb_pat",
            RepositoryPath = "alice/repo",
            Operation = "download",
            Transfers = ["basic"],
            Objects = oids.Select(oid => new LfsBatchObjectRequest { Oid = oid, Size = 42 }).ToList(),
            LfsBaseUrl = BaseUrl,
        };

    private static LfsBatchRequest UploadBatch(params string[] oids) =>
        new()
        {
            AccessToken = "ogb_pat",
            RepositoryPath = "alice/repo",
            Operation = "upload",
            Objects = oids.Select(oid => new LfsBatchObjectRequest { Oid = oid, Size = 5_000_000 }).ToList(),
            LfsBaseUrl = BaseUrl,
        };

    private static LfsTransferCheckRequest TransferCheck(string token, string oid, string operation) =>
        new()
        {
            Token = token,
            Oid = oid,
            Operation = operation,
            RepositoryPath = "alice/repo",
        };

    private static LfsRepairRequest Repair(string token, string oid) =>
        new()
        {
            Token = token,
            Oid = oid,
            RepositoryPath = "alice/repo",
        };

    private static LfsVerifyRequest Verify(string token, string oid, long size) =>
        new()
        {
            Token = token,
            Oid = oid,
            Size = size,
            RepositoryPath = "alice/repo",
        };

    private static RepositoryReplicationPeerDto Peer(string host, string role, bool healthy) =>
        new()
        {
            StorageNodeId = host == "storage-1" ? Guid.Empty : FakeLfsStorageNodes.NodeId(host),
            InternalHost = host,
            InternalHttpPort = 8081,
            Role = role,
            IsHealthy = healthy,
        };

    private LfsService CreateService() =>
        new(
            _lfsOptions,
            new GitOptions { PublicBaseUrl = "https://api.example.com" },
            new RepositoryAccessCheckEvaluator(
                _queryProcessor,
                Substitute.For<ISshKeyService>(),
                new GitPushEnforcementService(
                    _queryProcessor,
                    Substitute.For<IStorageContentClient>(),
                    new WebReadReplicaSelector()
                ),
                NullLogger.Instance,
                _quotaOptions,
                new PlatformMergeIdentityOptions()
            ),
            _quotaOptions,
            _queryProcessor,
            CreateTokenService(),
            _nodes.CreateClient(),
            new LfsReplicationService(
                _queryProcessor,
                _nodes.CreateClient(),
                _keys,
                _lfsOptions,
                NullLogger<LfsReplicationService>.Instance
            ),
            _completions,
            _repairs,
            NullLogger<LfsService>.Instance
        );

    private LfsActionTokenService CreateTokenService() =>
        new(new JwtOptions { Key = new string('k', 64) }, _lfsOptions, _clock);

    private string IssueToken(string oid, long size, LfsTransferOperation operation) =>
        CreateTokenService().Issue(_repositoryId.Value, oid, size, operation, _ownerUserId.Value).Token;

    private void ConfigureCaller(UserId caller)
    {
        _queryProcessor
            .RunQueryAsync(Arg.Any<ValidateGitAccessTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(new ValidateGitAccessTokenResult { UserId = caller, Scope = GitAccessTokenScopes.Write }));
    }

    private void ConfigureMember(RepositoryRole role)
    {
        var member = UserId.From(Guid.NewGuid());
        ConfigureCaller(member);
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto { RepositoryId = _repositoryId, UserId = member, Role = role }
                )
            );
    }

    /// <summary>Makes the repository organization-owned; returns a user with <paramref name="role"/> in it.</summary>
    private UserId ConfigureOrganizationRepository(OrganizationMemberRole role)
    {
        var member = UserId.From(Guid.NewGuid());
        _repository.OwnerKind = "organization";
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new OrganizationDto
                    {
                        Id = OrganizationId.From(_ownerUserId.Value),
                        Name = "Acme",
                        Slug = "alice",
                        OwnerUserId = Guid.NewGuid(),
                    }
                )
            );
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new OrganizationMemberDto
                    {
                        OrganizationId = OrganizationId.From(_ownerUserId.Value),
                        UserId = member,
                        Role = role,
                    }
                )
            );
        ConfigureCaller(member);
        return member;
    }

    private void ConfigureOwnerWithToken(string scope)
    {
        _queryProcessor
            .RunQueryAsync(Arg.Any<ValidateGitAccessTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(new ValidateGitAccessTokenResult { UserId = _ownerUserId, Scope = scope }));
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => Option.From(_repository));
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<OrganizationDto>.None);
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryMemberDto>.None);
        _queryProcessor
            .RunQueryAsync(Arg.Any<UserExistsByUsernameQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(_ownerUserId));
        _queryProcessor
            .RunQueryAsync(Arg.Any<RepositoryReplicationRoutingQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
                Option.From(
                    new RepositoryReplicationRoutingDto
                    {
                        ReplicationEpoch = 1,
                        WriteQuorumAvailable = _primaryHealthy,
                        Targets =
                        [
                            new RepositoryRoutingTargetDto
                            {
                                StorageNodeId = _primaryNodeId,
                                InternalHost = "storage-1",
                                InternalSshPort = 22,
                                InternalGitHttpPort = 8082,
                                InternalHttpPort = 8081,
                                Role = "Primary",
                                IsHealthy = _primaryHealthy,
                                IsInSync = _primaryHealthy,
                                IsPrimary = true,
                            },
                            new RepositoryRoutingTargetDto
                            {
                                StorageNodeId = FakeLfsStorageNodes.NodeId("storage-4"),
                                InternalHost = "storage-4",
                                InternalSshPort = 22,
                                InternalGitHttpPort = 8082,
                                InternalHttpPort = 8081,
                                Role = "ReadReplica",
                                IsHealthy = _readReplicaHealthy,
                                IsInSync = _readReplicaHealthy,
                                IsPrimary = false,
                            },
                        ],
                    }
                )
            );
    }
}
