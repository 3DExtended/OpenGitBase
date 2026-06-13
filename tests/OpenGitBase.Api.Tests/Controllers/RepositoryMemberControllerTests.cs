using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Controllers;

public class RepositoryMemberControllerTests
{
    [Fact]
    public async Task Create_WhenRepositoryMissing_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);

        var controller = CreateController(queryProcessor, userId);
        var request = CreateMemberRequest(
            RepositoryId.From(Guid.NewGuid()),
            UserId.From(Guid.NewGuid())
        );

        var result = await controller.Create(request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Create_WhenOwner_ReturnsCreatedWithId()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var memberUserId = UserId.From(Guid.NewGuid());
        var createdMemberId = RepositoryMemberId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = userId,
                        Slug = "owned",
                        Name = "Owned",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<CreateRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(createdMemberId));

        var controller = CreateController(queryProcessor, userId);
        var request = CreateMemberRequest(repositoryId, memberUserId, RepositoryRole.Writer);

        var result = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(RepositoryMemberController.Create), created.ActionName);
        Assert.Equal(createdMemberId, created.Value);

        await queryProcessor
            .DidNotReceive()
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WhenNonOwnerWithoutMembership_ReturnsForbid()
    {
        var userId = UserId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = ownerUserId,
                        Slug = "protected",
                        Name = "Protected",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryMemberDto>.None);

        var controller = CreateController(queryProcessor, userId);
        var request = CreateMemberRequest(repositoryId, UserId.From(Guid.NewGuid()));

        var result = await controller.Create(request, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Theory]
    [InlineData(RepositoryRole.None)]
    [InlineData(RepositoryRole.Reader)]
    [InlineData(RepositoryRole.Writer)]
    public async Task Create_WhenNonOwnerWithInsufficientRole_ReturnsForbid(RepositoryRole role)
    {
        var userId = UserId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = ownerUserId,
                        Slug = "protected",
                        Name = "Protected",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        Id = RepositoryMemberId.From(Guid.NewGuid()),
                        RepositoryId = repositoryId,
                        UserId = userId,
                        Role = role,
                    }
                )
            );

        var controller = CreateController(queryProcessor, userId);
        var request = CreateMemberRequest(repositoryId, UserId.From(Guid.NewGuid()));

        var result = await controller.Create(request, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Create_WhenAdminMember_ReturnsCreatedWithId()
    {
        var userId = UserId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var createdMemberId = RepositoryMemberId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = ownerUserId,
                        Slug = "shared",
                        Name = "Shared",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        Id = RepositoryMemberId.From(Guid.NewGuid()),
                        RepositoryId = repositoryId,
                        UserId = userId,
                        Role = RepositoryRole.Admin,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<CreateRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(createdMemberId));

        var controller = CreateController(queryProcessor, userId);
        var request = CreateMemberRequest(repositoryId, UserId.From(Guid.NewGuid()));

        var result = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(createdMemberId, created.Value);
    }

    [Fact]
    public async Task Create_WhenCreateQueryFails_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = userId,
                        Slug = "owned",
                        Name = "Owned",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<CreateRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryMemberId>.None);

        var controller = CreateController(queryProcessor, userId);
        var request = CreateMemberRequest(repositoryId, UserId.From(Guid.NewGuid()));

        var result = await controller.Create(request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task List_WhenFound_ReturnsOkWithMembers()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var members = new List<RepositoryMemberDto>
        {
            new()
            {
                Id = RepositoryMemberId.From(Guid.NewGuid()),
                RepositoryId = repositoryId,
                UserId = userId,
                Role = RepositoryRole.Admin,
            },
        };
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(
                Arg.Is<ListRepositoryMemberQuery>(query => query.RepositoryId == repositoryId),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option.From((IReadOnlyList<RepositoryMemberDto>)members));

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.List(repositoryId.Value, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<List<RepositoryMemberDto>>(ok.Value);
        Assert.Single(returned);
        Assert.Equal(RepositoryRole.Admin, returned[0].Role);
    }

    [Fact]
    public async Task List_WhenQueryReturnsNone_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<IReadOnlyList<RepositoryMemberDto>>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.List(repositoryId.Value, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_WhenExistingMemberMissing_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryMemberDto>.None);

        var controller = CreateController(queryProcessor, userId);
        var request = CreateUpdateRequest(
            RepositoryMemberId.From(Guid.NewGuid()),
            RepositoryId.From(Guid.NewGuid()),
            UserId.From(Guid.NewGuid())
        );

        var result = await controller.Update(
            request.UpdatedModel.Id.Value,
            request,
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_WhenRouteIdMismatch_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var memberUserId = UserId.From(Guid.NewGuid());
        var existingMemberId = RepositoryMemberId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        Id = existingMemberId,
                        RepositoryId = repositoryId,
                        UserId = memberUserId,
                        Role = RepositoryRole.Writer,
                    }
                )
            );

        var controller = CreateController(queryProcessor, userId);
        var request = CreateUpdateRequest(
            RepositoryMemberId.From(Guid.NewGuid()),
            repositoryId,
            memberUserId
        );

        var result = await controller.Update(
            request.UpdatedModel.Id.Value,
            request,
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_WhenRepositoryMissing_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var memberId = RepositoryMemberId.From(Guid.NewGuid());
        var memberUserId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        Id = memberId,
                        RepositoryId = repositoryId,
                        UserId = memberUserId,
                        Role = RepositoryRole.Writer,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);

        var controller = CreateController(queryProcessor, userId);
        var request = CreateUpdateRequest(memberId, repositoryId, memberUserId);

        var result = await controller.Update(memberId.Value, request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_WhenNonOwnerWithoutAdmin_ReturnsForbid()
    {
        var userId = UserId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var memberId = RepositoryMemberId.From(Guid.NewGuid());
        var memberUserId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var query = callInfo.Arg<GetRepositoryMemberQuery>();
                if (query.UserId == memberUserId)
                {
                    return Option.From(
                        new RepositoryMemberDto
                        {
                            Id = memberId,
                            RepositoryId = repositoryId,
                            UserId = memberUserId,
                            Role = RepositoryRole.Writer,
                        }
                    );
                }

                return Option<RepositoryMemberDto>.None;
            });
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = ownerUserId,
                        Slug = "protected",
                        Name = "Protected",
                    }
                )
            );

        var controller = CreateController(queryProcessor, userId);
        var request = CreateUpdateRequest(memberId, repositoryId, memberUserId);

        var result = await controller.Update(memberId.Value, request, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Update_WhenOwner_ReturnsNoContent()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var memberId = RepositoryMemberId.From(Guid.NewGuid());
        var memberUserId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        Id = memberId,
                        RepositoryId = repositoryId,
                        UserId = memberUserId,
                        Role = RepositoryRole.Writer,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = userId,
                        Slug = "owned",
                        Name = "Owned",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<UpdateRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(Unit.Value));

        var controller = CreateController(queryProcessor, userId);
        var request = CreateUpdateRequest(
            memberId,
            repositoryId,
            memberUserId,
            RepositoryRole.Admin
        );

        var result = await controller.Update(memberId.Value, request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);

        await queryProcessor
            .Received(1)
            .RunQueryAsync(
                Arg.Is<UpdateRepositoryMemberQuery>(query =>
                    query.UpdatedModel.Id == memberId
                    && query.UpdatedModel.Role == RepositoryRole.Admin
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Update_WhenUpdateQueryFails_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var memberId = RepositoryMemberId.From(Guid.NewGuid());
        var memberUserId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        Id = memberId,
                        RepositoryId = repositoryId,
                        UserId = memberUserId,
                        Role = RepositoryRole.Writer,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = userId,
                        Slug = "owned",
                        Name = "Owned",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<UpdateRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<Unit>.None);

        var controller = CreateController(queryProcessor, userId);
        var request = CreateUpdateRequest(memberId, repositoryId, memberUserId);

        var result = await controller.Update(memberId.Value, request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_WhenAdminMember_ReturnsNoContent()
    {
        var userId = UserId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var memberId = RepositoryMemberId.From(Guid.NewGuid());
        var memberUserId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var query = callInfo.Arg<GetRepositoryMemberQuery>();
                if (query.UserId == memberUserId)
                {
                    return Option.From(
                        new RepositoryMemberDto
                        {
                            Id = memberId,
                            RepositoryId = repositoryId,
                            UserId = memberUserId,
                            Role = RepositoryRole.Writer,
                        }
                    );
                }

                return Option.From(
                    new RepositoryMemberDto
                    {
                        Id = RepositoryMemberId.From(Guid.NewGuid()),
                        RepositoryId = repositoryId,
                        UserId = userId,
                        Role = RepositoryRole.Admin,
                    }
                );
            });
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = ownerUserId,
                        Slug = "shared",
                        Name = "Shared",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<UpdateRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(Unit.Value));

        var controller = CreateController(queryProcessor, userId);
        var request = CreateUpdateRequest(memberId, repositoryId, memberUserId);

        var result = await controller.Update(memberId.Value, request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_WhenAdminMember_ReturnsNoContent()
    {
        var userId = UserId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var memberId = RepositoryMemberId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        Id = memberId,
                        RepositoryId = repositoryId,
                        UserId = UserId.From(Guid.NewGuid()),
                        Role = RepositoryRole.Writer,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = ownerUserId,
                        Slug = "shared",
                        Name = "Shared",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        Id = RepositoryMemberId.From(Guid.NewGuid()),
                        RepositoryId = repositoryId,
                        UserId = userId,
                        Role = RepositoryRole.Admin,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<DeleteRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(Unit.Value));

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Delete(memberId.Value, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_WhenMemberMissing_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryMemberDto>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_WhenRepositoryMissing_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var memberId = RepositoryMemberId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        Id = memberId,
                        RepositoryId = repositoryId,
                        UserId = UserId.From(Guid.NewGuid()),
                        Role = RepositoryRole.Writer,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Delete(memberId.Value, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_WhenNonOwnerWithoutAdmin_ReturnsForbid()
    {
        var userId = UserId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var memberId = RepositoryMemberId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        Id = memberId,
                        RepositoryId = repositoryId,
                        UserId = UserId.From(Guid.NewGuid()),
                        Role = RepositoryRole.Writer,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = ownerUserId,
                        Slug = "protected",
                        Name = "Protected",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryMemberDto>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Delete(memberId.Value, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Delete_WhenOwner_ReturnsNoContent()
    {
        var userId = UserId.From(Guid.NewGuid());
        var memberId = RepositoryMemberId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        Id = memberId,
                        RepositoryId = repositoryId,
                        UserId = UserId.From(Guid.NewGuid()),
                        Role = RepositoryRole.Writer,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = userId,
                        Slug = "owned",
                        Name = "Owned",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<DeleteRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(Unit.Value));

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Delete(memberId.Value, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);

        await queryProcessor
            .Received(1)
            .RunQueryAsync(
                Arg.Is<DeleteRepositoryMemberQuery>(query => query.Id == memberId),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Delete_WhenDeleteQueryFails_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var memberId = RepositoryMemberId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        Id = memberId,
                        RepositoryId = repositoryId,
                        UserId = UserId.From(Guid.NewGuid()),
                        Role = RepositoryRole.Writer,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = userId,
                        Slug = "owned",
                        Name = "Owned",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<DeleteRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<Unit>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Delete(memberId.Value, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private static CreateRepositoryMemberQuery CreateMemberRequest(
        RepositoryId repositoryId,
        UserId memberUserId,
        RepositoryRole role = RepositoryRole.Reader
    ) =>
        new()
        {
            ModelToCreate = new RepositoryMemberDto
            {
                RepositoryId = repositoryId,
                UserId = memberUserId,
                Role = role,
            },
        };

    private static UpdateRepositoryMemberQuery CreateUpdateRequest(
        RepositoryMemberId id,
        RepositoryId repositoryId,
        UserId userId,
        RepositoryRole role = RepositoryRole.Writer
    ) =>
        new()
        {
            UpdatedModel = new RepositoryMemberDto
            {
                Id = id,
                RepositoryId = repositoryId,
                UserId = userId,
                Role = role,
            },
        };

    private static RepositoryMemberController CreateController(
        IQueryProcessor queryProcessor,
        UserId userId,
        string username = "testuser"
    )
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.User.Returns(
            new UserIdentity { IdentityProviderId = userId.Value.ToString(), Username = username }
        );

        return new RepositoryMemberController(queryProcessor, userContext);
    }
}
