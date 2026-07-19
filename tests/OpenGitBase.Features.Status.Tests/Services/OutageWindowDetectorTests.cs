using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Services;

namespace OpenGitBase.Features.Status.Tests.Services;

public class OutageWindowDetectorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Apply_DoesNotOpen_BeforeFiveMinutesUnhealthy()
    {
        var tracking = new OutageWindowRecord
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeee0"),
            Scope = OutageWindowScope.Group,
            Group = StatusComponentGroup.MessageBus,
            UnhealthySince = T0,
            BecamePublicAt = null,
            EndedAt = null,
            DisplayName = "Message bus",
        };

        var at4m = OutageWindowDetector.Apply(
            [tracking],
            observations: [GroupObs(StatusComponentGroup.MessageBus, PublicHealthStatus.Unhealthy)],
            now: T0.AddMinutes(4)
        );

        Assert.Single(at4m.Upserts);
        Assert.Null(at4m.Upserts[0].BecamePublicAt);
        Assert.Null(at4m.Upserts[0].EndedAt);
    }

    [Fact]
    public void Apply_Opens_AfterFiveMinutesUnhealthy()
    {
        var tracking = new OutageWindowRecord
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeee1"),
            Scope = OutageWindowScope.Group,
            Group = StatusComponentGroup.MessageBus,
            InstanceId = null,
            UnhealthySince = T0,
            BecamePublicAt = null,
            EndedAt = null,
            LastNonUnhealthyAt = null,
            DisplayName = "Message bus",
        };

        var result = OutageWindowDetector.Apply(
            [tracking],
            observations: [GroupObs(StatusComponentGroup.MessageBus, PublicHealthStatus.Unhealthy)],
            now: T0.AddMinutes(5)
        );

        Assert.Single(result.Upserts);
        Assert.Equal(T0.AddMinutes(5), result.Upserts[0].BecamePublicAt);
        Assert.Null(result.Upserts[0].EndedAt);
        Assert.Contains(result.Logs, l => l.Contains("opened", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Apply_DegradedOnly_NeverOpens()
    {
        var result = OutageWindowDetector.Apply(
            [],
            observations: [GroupObs(StatusComponentGroup.Api, PublicHealthStatus.Degraded)],
            now: T0.AddMinutes(10)
        );

        Assert.Empty(result.Upserts);
        Assert.Empty(result.Deletes);
    }

    [Fact]
    public void Apply_DoesNotCreateOverallWindows()
    {
        var result = OutageWindowDetector.Apply(
            [],
            observations: [GroupObs(StatusComponentGroup.Overall, PublicHealthStatus.Unhealthy)],
            now: T0.AddMinutes(10)
        );

        Assert.Empty(result.Upserts);
    }

    [Fact]
    public void Apply_MergesHealthyGap_OfTwoMinutesOrLess()
    {
        var open = PublicGroupWindow(StatusComponentGroup.Git, T0, becamePublicAt: T0.AddMinutes(5));
        open.LastNonUnhealthyAt = T0.AddMinutes(10);

        var result = OutageWindowDetector.Apply(
            [open],
            observations: [GroupObs(StatusComponentGroup.Git, PublicHealthStatus.Unhealthy)],
            now: T0.AddMinutes(12) // 2 min healthy gap
        );

        Assert.Single(result.Upserts);
        Assert.Null(result.Upserts[0].EndedAt);
        Assert.Null(result.Upserts[0].LastNonUnhealthyAt);
    }

    [Fact]
    public void Apply_Closes_AfterHealthyGapOverTwoMinutes()
    {
        var open = PublicGroupWindow(StatusComponentGroup.Git, T0, becamePublicAt: T0.AddMinutes(5));
        open.LastNonUnhealthyAt = T0.AddMinutes(10);

        var result = OutageWindowDetector.Apply(
            [open],
            observations: [GroupObs(StatusComponentGroup.Git, PublicHealthStatus.Healthy)],
            now: T0.AddMinutes(12).AddSeconds(1) // >2 min healthy
        );

        Assert.Single(result.Upserts);
        Assert.Equal(T0.AddMinutes(12).AddSeconds(1), result.Upserts[0].EndedAt);
        Assert.Contains(result.Logs, l => l.Contains("closed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Apply_DataStores_UsesInstanceHeadline_NotGroup()
    {
        var result = OutageWindowDetector.Apply(
            [],
            observations:
            [
                new OutageHealthObservation
                {
                    Scope = OutageWindowScope.Instance,
                    Group = StatusComponentGroup.DataStores,
                    InstanceId = "postgres",
                    Status = PublicHealthStatus.Unhealthy,
                    GroupStatus = PublicHealthStatus.Unhealthy,
                },
            ],
            now: T0
        );

        Assert.Single(result.Upserts);
        Assert.Equal(OutageWindowScope.Instance, result.Upserts[0].Scope);
        Assert.Equal("postgres", result.Upserts[0].InstanceId);
        Assert.Equal("Postgres", result.Upserts[0].DisplayName);
    }

    [Fact]
    public void Apply_PartialInstance_WhenGroupNotUnhealthy()
    {
        var result = OutageWindowDetector.Apply(
            [],
            observations:
            [
                new OutageHealthObservation
                {
                    Scope = OutageWindowScope.Instance,
                    Group = StatusComponentGroup.MessageBus,
                    InstanceId = "broker-1",
                    Status = PublicHealthStatus.Unhealthy,
                    GroupStatus = PublicHealthStatus.Degraded,
                },
            ],
            now: T0
        );

        Assert.Single(result.Upserts);
        Assert.Equal(OutageWindowScope.Instance, result.Upserts[0].Scope);
        Assert.True(result.Upserts[0].IsPartial);
    }

    [Fact]
    public void Apply_DeletesProvisional_WhenHealthyBeforePublic()
    {
        var tracking = new OutageWindowRecord
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeee2"),
            Scope = OutageWindowScope.Group,
            Group = StatusComponentGroup.Website,
            UnhealthySince = T0,
            BecamePublicAt = null,
            EndedAt = null,
            LastNonUnhealthyAt = T0.AddMinutes(1),
            DisplayName = "Website",
        };

        var result = OutageWindowDetector.Apply(
            [tracking],
            observations: [GroupObs(StatusComponentGroup.Website, PublicHealthStatus.Healthy)],
            now: T0.AddMinutes(4) // >2 min healthy before public
        );

        Assert.Contains(tracking.Id, result.Deletes);
        Assert.Empty(result.Upserts);
    }

    private static OutageHealthObservation GroupObs(
        StatusComponentGroup group,
        PublicHealthStatus status
    ) =>
        new()
        {
            Scope = OutageWindowScope.Group,
            Group = group,
            InstanceId = null,
            Status = status,
            GroupStatus = status,
        };

    private static OutageWindowRecord PublicGroupWindow(
        StatusComponentGroup group,
        DateTimeOffset unhealthySince,
        DateTimeOffset becamePublicAt
    ) =>
        new()
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeee3"),
            Scope = OutageWindowScope.Group,
            Group = group,
            UnhealthySince = unhealthySince,
            BecamePublicAt = becamePublicAt,
            EndedAt = null,
            DisplayName = group.ToString(),
        };
}
