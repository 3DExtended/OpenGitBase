using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using NSubstitute;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Tests.Controllers;

public class OrganizationPipelineControllerTests : ControllerTestBase
{
    public OrganizationPipelineControllerTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory) { }

    [Fact]
    public async Task ApproveDomainRequest_AsOwner_LimitsTheReviewToTheOrganizationInTheRoute()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = Guid.NewGuid();
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new OrganizationDto
                    {
                        Id = OrganizationId.From(organizationId),
                        Name = "Org",
                        Slug = "org",
                        OwnerUserId = userId.Value,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<ReviewDomainAllowanceRequestQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(new DomainAllowanceRequestDto()));
        var client = CreateAuthenticatedClient(queryProcessor, userId, "org-owner");

        var response = await client.PostAsync(
            $"/organization/{organizationId}/pipeline/egress/domain-requests/{Guid.NewGuid()}/approve",
            null
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await queryProcessor
            .Received(1)
            .RunQueryAsync(
                Arg.Is<ReviewDomainAllowanceRequestQuery>(query => query.OrganizationId == organizationId),
                Arg.Any<CancellationToken>()
            );
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("deny")]
    public async Task ReviewDomainRequest_ThroughTheOrganizationsRouteWithoutOwnership_DoesNotReviewTheRequest(
        string action
    )
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ReviewDomainAllowanceRequestQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(new DomainAllowanceRequestDto()));
        var client = CreateAuthenticatedClient(queryProcessor, userId, "not-an-owner");

        var response = await client.PostAsync(
            $"/organizations/{Guid.NewGuid()}/pipeline/egress/domain-requests/{Guid.NewGuid()}/{action}",
            null
        );

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        await queryProcessor
            .DidNotReceive()
            .RunQueryAsync(Arg.Any<ReviewDomainAllowanceRequestQuery>(), Arg.Any<CancellationToken>());
    }

    private HttpClient CreateAuthenticatedClient(IQueryProcessor queryProcessor, UserId userId, string username)
    {
        queryProcessor
            .RunQueryAsync(Arg.Any<UserGetByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(new User { Id = userId, Username = username }));
        var (client, _) = CreateClientWithQueryProcessor(queryProcessor);
        SeedTokenUser(userId.Value, username);
        var token = JwtTokenGenerator.GetJWTToken(username, userId.Value.ToString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
