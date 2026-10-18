using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Tests.Controllers;

public class RepositoryInternalPushValidationControllerTests
{
    private const string RecordedOid = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string MissingOid = "2222222222222222222222222222222222222222222222222222222222222222";
    private const string OtherMissingOid = "3333333333333333333333333333333333333333333333333333333333333333";

    [Fact]
    public async Task ValidatePush_WhenPushRulesOnlyAndDcoMissing_ReturnsDenied()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        PhysicalPath = $"/srv/git/{repositoryId.Value}.git",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<ListProtectedBranchRulesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<ProtectedBranchRuleDto>>(
                    [
                        new ProtectedBranchRuleDto
                        {
                            RepositoryId = repositoryId,
                            Pattern = "main",
                            PushRules =
                            [
                                new PushRuleDto
                                {
                                    RuleType = PushRuleType.RequireDco,
                                    ConfigJson = """{"required":true}""",
                                },
                            ],
                        },
                    ]
                )
            );

        var controller = new RepositoryInternalPushValidationController(
            queryProcessor,
            new GitPushEnforcementService(
                queryProcessor,
                Substitute.For<IStorageContentClient>(),
                new WebReadReplicaSelector()
            )
        );

        var result = await controller.ValidatePush(
            new RepositoryPushValidationRequest
            {
                PhysicalPath = $"/srv/git/{repositoryId.Value}.git",
                ValidatePushRulesOnly = true,
                Commits =
                [
                    new GitPushCommitRequest
                    {
                        Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        Message = "missing signoff",
                    },
                ],
            },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryPushValidationResponse>(ok.Value);
        Assert.False(response.Allowed);
        Assert.Contains("RequireDco", response.Reason);
    }

    [Fact]
    public async Task ValidatePush_WithAPointerToAnUnrecordedObject_RejectsWithTheOidPathAndFix()
    {
        var (controller, repositoryId) = CreateLfsController();

        var response = await ValidateAsync(
            controller,
            repositoryId,
            [Pointer(RecordedOid, "assets/kept.bin"), Pointer(MissingOid, "assets/big.bin")]
        );

        Assert.False(response.Allowed);
        Assert.Equal(
            $"LFS object {MissingOid} (assets/big.bin) is missing on the server. Run: git lfs push --all origin main",
            response.Reason
        );
    }

    [Fact]
    public async Task ValidatePush_WithSeveralMissingObjects_NamesTheFirstAndCountsTheRest()
    {
        var (controller, repositoryId) = CreateLfsController();

        var response = await ValidateAsync(
            controller,
            repositoryId,
            [Pointer(MissingOid, "a.bin"), Pointer(OtherMissingOid, "b.bin")]
        );

        Assert.False(response.Allowed);
        Assert.Equal(
            $"LFS object {MissingOid} (a.bin) and 1 more are missing on the server. Run: git lfs push --all origin main",
            response.Reason
        );
    }

    [Fact]
    public async Task ValidatePush_WhenEveryPointedObjectIsRecorded_Allows()
    {
        var (controller, repositoryId) = CreateLfsController();

        var response = await ValidateAsync(controller, repositoryId, [Pointer(RecordedOid, "assets/kept.bin")]);

        Assert.True(response.Allowed);
    }

    [Fact]
    public async Task ValidatePush_WithAnOldHookPayloadWithoutPointers_StillValidates()
    {
        var (controller, repositoryId) = CreateLfsController();
        var request = JsonSerializer.Deserialize<RepositoryPushValidationRequest>(
            $$"""
            {"physicalPath":"/srv/git/{{repositoryId.Value}}.git","validatePushRulesOnly":true,
             "refUpdates":[],"commits":[{"sha":"{{new string('a', 40)}}","message":"m","changedPaths":[],"maxBlobBytes":1}]}
            """,
            JsonSerializerOptions.Web
        )!;

        var result = await controller.ValidatePush(request, CancellationToken.None);

        var response = Assert.IsType<RepositoryPushValidationResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(response.Allowed);
        Assert.Empty(request.LfsPointers);
    }

    private static GitPushLfsPointerRequest Pointer(string oid, string path) =>
        new()
        {
            Oid = oid,
            Size = 10,
            Path = path,
            RefName = "refs/heads/main",
        };

    private static (RepositoryInternalPushValidationController Controller, RepositoryId RepositoryId) CreateLfsController()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto { Id = repositoryId, PhysicalPath = $"/srv/git/{repositoryId.Value}.git" }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<ListProtectedBranchRulesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<IReadOnlyList<ProtectedBranchRuleDto>>.None);
        queryProcessor
            .RunQueryAsync(Arg.Any<FindLfsObjectsQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var query = callInfo.Arg<FindLfsObjectsQuery>();
                IReadOnlyList<LfsObjectDto> recorded = query.RepositoryId == repositoryId && query.Oids.Contains(RecordedOid)
                    ? [new LfsObjectDto { Oid = RecordedOid, Size = 10 }]
                    : [];
                return Option.From(recorded);
            });
        var controller = new RepositoryInternalPushValidationController(
            queryProcessor,
            new GitPushEnforcementService(
                queryProcessor,
                Substitute.For<IStorageContentClient>(),
                new WebReadReplicaSelector()
            )
        );
        return (controller, repositoryId);
    }

    private static async Task<RepositoryPushValidationResponse> ValidateAsync(
        RepositoryInternalPushValidationController controller,
        RepositoryId repositoryId,
        IReadOnlyList<GitPushLfsPointerRequest> pointers
    )
    {
        var result = await controller.ValidatePush(
            new RepositoryPushValidationRequest
            {
                PhysicalPath = $"/srv/git/{repositoryId.Value}.git",
                ValidatePushRulesOnly = true,
                Commits = [new GitPushCommitRequest { Sha = new string('a', 40), Message = "m" }],
                LfsPointers = pointers,
            },
            CancellationToken.None
        );
        return Assert.IsType<RepositoryPushValidationResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }
}
