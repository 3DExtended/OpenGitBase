using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Services;

public class OrganizationAccessServiceTests
{
    [Fact]
    public async Task CheckOwnerAccessAsync_WhenOrganizationMissing_ReturnsNotFound()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<OrganizationDto>.None);

        var service = new OrganizationAccessService(queryProcessor);
        var result = await service.CheckOwnerAccessAsync(
            OrganizationId.From(Guid.NewGuid()),
            UserId.From(Guid.NewGuid()),
            CancellationToken.None
        );

        Assert.False(result.OrganizationExists);
        Assert.False(result.IsOwner);
        Assert.Null(result.Organization);
    }

    [Fact]
    public async Task CheckOwnerAccessAsync_WhenUserIsDirectOwner_ReturnsOwner()
    {
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var organization = new OrganizationDto
        {
            Id = organizationId,
            Name = "Acme",
            Slug = "acme",
            OwnerUserId = ownerUserId.Value,
        };

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(organization));

        var service = new OrganizationAccessService(queryProcessor);
        var result = await service.CheckOwnerAccessAsync(
            organizationId,
            ownerUserId,
            CancellationToken.None
        );

        Assert.True(result.OrganizationExists);
        Assert.True(result.IsOwner);
        Assert.Equal(organization, result.Organization);
    }

    [Fact]
    public async Task CheckOwnerAccessAsync_WhenUserIsOwnerMember_ReturnsOwner()
    {
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var memberUserId = UserId.From(Guid.NewGuid());
        var organization = new OrganizationDto
        {
            Id = organizationId,
            Name = "Acme",
            Slug = "acme",
            OwnerUserId = ownerUserId.Value,
        };

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(organization));
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new OrganizationMemberDto
                    {
                        OrganizationId = organizationId,
                        UserId = memberUserId,
                        Role = OrganizationMemberRole.Owner,
                    }
                )
            );

        var service = new OrganizationAccessService(queryProcessor);
        var result = await service.CheckOwnerAccessAsync(
            organizationId,
            memberUserId,
            CancellationToken.None
        );

        Assert.True(result.IsOwner);
    }

    [Fact]
    public async Task CheckMemberAccessAsync_WhenUserIsRegularMember_ReturnsMember()
    {
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var memberUserId = UserId.From(Guid.NewGuid());
        var organization = new OrganizationDto
        {
            Id = organizationId,
            Name = "Acme",
            Slug = "acme",
            OwnerUserId = Guid.NewGuid(),
        };

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(organization));
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new OrganizationMemberDto
                    {
                        OrganizationId = organizationId,
                        UserId = memberUserId,
                        Role = OrganizationMemberRole.Member,
                    }
                )
            );

        var service = new OrganizationAccessService(queryProcessor);
        var result = await service.CheckMemberAccessAsync(
            organizationId,
            memberUserId,
            CancellationToken.None
        );

        Assert.True(result.OrganizationExists);
        Assert.True(result.IsMember);
        Assert.False(result.IsOwner);
    }

    [Fact]
    public async Task CheckMemberAccessAsync_WhenUserIsNotMember_ReturnsNotMember()
    {
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var organization = new OrganizationDto
        {
            Id = organizationId,
            Name = "Acme",
            Slug = "acme",
            OwnerUserId = Guid.NewGuid(),
        };

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(organization));
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<OrganizationMemberDto>.None);

        var service = new OrganizationAccessService(queryProcessor);
        var result = await service.CheckMemberAccessAsync(
            organizationId,
            UserId.From(Guid.NewGuid()),
            CancellationToken.None
        );

        Assert.True(result.OrganizationExists);
        Assert.False(result.IsMember);
        Assert.False(result.IsOwner);
    }

    [Fact]
    public async Task GetDeleteBlockersAsync_WhenRepositoriesExist_ReturnsBlockers()
    {
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var repositories = (IReadOnlyList<RepositoryDto>)new List<RepositoryDto>
            {
                new()
                {
                    Id = RepositoryId.From(Guid.NewGuid()),
                    Name = "App",
                    Slug = "app",
                    OwnerUserId = UserId.From(organizationId.Value),
                },
            };
        queryProcessor
            .RunQueryAsync(Arg.Any<ListRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repositories));

        var service = new OrganizationAccessService(queryProcessor);
        var blockers = await service.GetDeleteBlockersAsync(organizationId, CancellationToken.None);

        var blocker = Assert.Single(blockers);
        Assert.Equal("repository", blocker.Type);
        Assert.Equal("App", blocker.Name);
    }

    [Fact]
    public async Task WouldRemoveLastOwnerAsync_WhenSingleOwner_ReturnsTrue()
    {
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var members = (IReadOnlyList<OrganizationMemberDto>)new List<OrganizationMemberDto>
            {
                new()
                {
                    OrganizationId = organizationId,
                    UserId = ownerUserId,
                    Role = OrganizationMemberRole.Owner,
                },
            };
        queryProcessor
            .RunQueryAsync(Arg.Any<ListOrganizationMembersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(members));

        var service = new OrganizationAccessService(queryProcessor);
        var result = await service.WouldRemoveLastOwnerAsync(
            organizationId,
            ownerUserId,
            CancellationToken.None
        );

        Assert.True(result);
    }

    [Fact]
    public async Task WouldRemoveLastOwnerAsync_WhenMemberIsNotOwner_ReturnsFalse()
    {
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var memberUserId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var members = (IReadOnlyList<OrganizationMemberDto>)new List<OrganizationMemberDto>
            {
                new()
                {
                    OrganizationId = organizationId,
                    UserId = memberUserId,
                    Role = OrganizationMemberRole.Member,
                },
            };
        queryProcessor
            .RunQueryAsync(Arg.Any<ListOrganizationMembersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(members));

        var service = new OrganizationAccessService(queryProcessor);
        var result = await service.WouldRemoveLastOwnerAsync(
            organizationId,
            memberUserId,
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task WouldDemoteLastOwnerAsync_WhenSingleOwner_ReturnsTrue()
    {
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var members = (IReadOnlyList<OrganizationMemberDto>)new List<OrganizationMemberDto>
        {
            new()
            {
                OrganizationId = organizationId,
                UserId = ownerUserId,
                Role = OrganizationMemberRole.Owner,
            },
        };
        queryProcessor
            .RunQueryAsync(Arg.Any<ListOrganizationMembersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(members));

        var service = new OrganizationAccessService(queryProcessor);
        var result = await service.WouldDemoteLastOwnerAsync(
            organizationId,
            ownerUserId,
            CancellationToken.None
        );

        Assert.True(result);
    }
}
