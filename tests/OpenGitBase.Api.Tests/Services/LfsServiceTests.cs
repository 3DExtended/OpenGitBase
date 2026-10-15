using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
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
    private readonly IStorageLfsClient _storage = Substitute.For<IStorageLfsClient>();
    private readonly ISystemClock _clock = Substitute.For<ISystemClock>();
    private readonly UserId _ownerUserId = UserId.From(Guid.NewGuid());
    private readonly RepositoryId _repositoryId = RepositoryId.From(Guid.NewGuid());
    private readonly Guid _primaryNodeId = Guid.NewGuid();
    private readonly LfsOptions _lfsOptions = new() { Enabled = true, ActionTokenLifetimeSeconds = 3600 };
    private readonly RepositoryStorageQuotaOptions _quotaOptions = new() { Enabled = true, MaxBytes = 1_073_741_824 };
    private readonly RepositoryDto _repository;
    private bool _primaryHealthy = true;

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
        _clock.UtcNow.Returns(_ => Now);
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
    public async Task VerifyAsync_WhenThePrimaryHoldsTheObject_RecordsIt()
    {
        var token = IssueToken(NewOid, 5_000_000, LfsTransferOperation.Upload);
        _storage
            .GetObjectSizeAsync(Arg.Any<StorageNodeEndpoint>(), _repositoryId.Value, NewOid, Arg.Any<CancellationToken>())
            .Returns(5_000_000L);

        var result = await CreateService().VerifyAsync(Verify(token, NewOid, 5_000_000), RequestId, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        await _queryProcessor
            .Received(1)
            .RunQueryAsync(
                Arg.Is<RecordLfsObjectQuery>(query =>
                    query.RepositoryId == _repositoryId
                    && query.Oid == NewOid
                    && query.Size == 5_000_000
                    && query.CreatedByUserId == _ownerUserId
                ),
                Arg.Any<CancellationToken>()
            );
        await _storage
            .Received(1)
            .GetObjectSizeAsync(
                Arg.Is<StorageNodeEndpoint>(node =>
                    node.StorageNodeId == _primaryNodeId
                    && node.InternalHost == "storage-1"
                    && node.InternalHttpPort == 8081
                    && node.ApiToken == "node-token"
                ),
                _repositoryId.Value,
                NewOid,
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task VerifyAsync_WhenThePrimaryLacksTheObject_Answers422AndRecordsNothing()
    {
        var token = IssueToken(NewOid, 5_000_000, LfsTransferOperation.Upload);
        _storage
            .GetObjectSizeAsync(Arg.Any<StorageNodeEndpoint>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((long?)null);

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

    private static LfsVerifyRequest Verify(string token, string oid, long size) =>
        new()
        {
            Token = token,
            Oid = oid,
            Size = size,
            RepositoryPath = "alice/repo",
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
            _storage,
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
                        ],
                    }
                )
            );
    }
}
