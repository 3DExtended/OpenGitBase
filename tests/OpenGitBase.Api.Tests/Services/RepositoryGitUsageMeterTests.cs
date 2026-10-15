using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OpenGitBase.Api.Models.StorageContent;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Services;

public class RepositoryGitUsageMeterTests
{
    private static readonly RepositoryId RepositoryId = RepositoryId.From(Guid.NewGuid());

    private static readonly RepositoryRoutingTargetDto Primary = new()
    {
        StorageNodeId = Guid.NewGuid(),
        InternalHost = "storage-2",
        InternalHttpPort = 8080,
        Role = "Primary",
        IsHealthy = true,
        IsPrimary = true,
    };

    private readonly IQueryProcessor _queryProcessor = Substitute.For<IQueryProcessor>();
    private readonly IStorageContentClient _storageContentClient = Substitute.For<IStorageContentClient>();

    public static TheoryData<Exception> StorageFailures() =>
        new()
        {
            new HttpRequestException("connection refused"),
            // What HttpClient throws when its own timeout (or the meter's) runs out.
            new TaskCanceledException("timed out"),
            new System.Text.Json.JsonException("not json"),
        };

    [Fact]
    public async Task MeasureStorageBytesUsedAsync_RecordsWhatThePrimaryReportsAndReturnsTheNewTotal()
    {
        GivenNodeToken("storage-token");
        GivenPrimaryReports(new StorageContentUsagePayload { BytesUsed = 4_000 });
        _queryProcessor
            .RunQueryAsync(Arg.Any<RecordRepositoryGitBytesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(4_500L));

        var total = await CreateMeter().MeasureStorageBytesUsedAsync(Repository(), Primary, CancellationToken.None);

        Assert.Equal(4_500, total);
        await _storageContentClient
            .Received(1)
            .GetDiskUsageAsync(Primary, "storage-token", Repository().PhysicalPath, Arg.Any<CancellationToken>());
        await _queryProcessor
            .Received(1)
            .RunQueryAsync(
                Arg.Is<GetStorageNodeApiTokenQuery>(query => query.StorageNodeId.Value == Primary.StorageNodeId),
                Arg.Any<CancellationToken>()
            );
        await _queryProcessor
            .Received(1)
            .RunQueryAsync(
                Arg.Is<RecordRepositoryGitBytesQuery>(query =>
                    query.RepositoryId == RepositoryId && query.GitBytes == 4_000
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task MeasureStorageBytesUsedAsync_WhenNodeTokenIsUnknown_ReturnsStoredCounterWithoutAskingStorage()
    {
        var total = await CreateMeter().MeasureStorageBytesUsedAsync(Repository(), Primary, CancellationToken.None);

        Assert.Equal(900, total);
        await _storageContentClient
            .DidNotReceiveWithAnyArgs()
            .GetDiskUsageAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task MeasureStorageBytesUsedAsync_WhenPrimaryDoesNotAnswer_ReturnsStoredCounterWithoutRecording()
    {
        GivenNodeToken("storage-token");
        GivenPrimaryReports(null);

        var total = await CreateMeter().MeasureStorageBytesUsedAsync(Repository(), Primary, CancellationToken.None);

        Assert.Equal(900, total);
        await AssertNothingRecordedAsync();
    }

    [Theory]
    [MemberData(nameof(StorageFailures))]
    public async Task MeasureStorageBytesUsedAsync_WhenAskingThePrimaryFails_ReturnsStoredCounter(Exception failure)
    {
        GivenNodeToken("storage-token");
        _storageContentClient
            .GetDiskUsageAsync(
                Arg.Any<RepositoryRoutingTargetDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(failure);

        var total = await CreateMeter().MeasureStorageBytesUsedAsync(Repository(), Primary, CancellationToken.None);

        Assert.Equal(900, total);
        await AssertNothingRecordedAsync();
    }

    [Fact]
    public async Task MeasureStorageBytesUsedAsync_WhenRepositoryVanishedBeforeRecording_ReturnsStoredCounter()
    {
        GivenNodeToken("storage-token");
        GivenPrimaryReports(new StorageContentUsagePayload { BytesUsed = 4_000 });

        var total = await CreateMeter().MeasureStorageBytesUsedAsync(Repository(), Primary, CancellationToken.None);

        Assert.Equal(900, total);
    }

    [Fact]
    public async Task MeasureStorageBytesUsedAsync_WhenCallerCancels_Throws()
    {
        GivenNodeToken("storage-token");
        using var cancellation = new CancellationTokenSource();
        _storageContentClient
            .GetDiskUsageAsync(
                Arg.Any<RepositoryRoutingTargetDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<StorageContentUsagePayload?>(cancellation.Token);
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateMeter().MeasureStorageBytesUsedAsync(Repository(), Primary, cancellation.Token)
        );
    }

    private static RepositoryDto Repository() =>
        new()
        {
            Id = RepositoryId,
            OwnerUserId = UserId.From(Guid.NewGuid()),
            Slug = "repo",
            Name = "Repo",
            PhysicalPath = $"/srv/git/{RepositoryId.Value}.git",
            StorageBytesUsed = 900,
        };

    private RepositoryGitUsageMeter CreateMeter() =>
        new(_queryProcessor, _storageContentClient, NullLogger<RepositoryGitUsageMeter>.Instance);

    private void GivenNodeToken(string token) =>
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeApiTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(token));

    private void GivenPrimaryReports(StorageContentUsagePayload? payload) =>
        _storageContentClient
            .GetDiskUsageAsync(
                Arg.Any<RepositoryRoutingTargetDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(payload);

    private async Task AssertNothingRecordedAsync() =>
        await _queryProcessor
            .DidNotReceive()
            .RunQueryAsync(Arg.Any<RecordRepositoryGitBytesQuery>(), Arg.Any<CancellationToken>());
}
