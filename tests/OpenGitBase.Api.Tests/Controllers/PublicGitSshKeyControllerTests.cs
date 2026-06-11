using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NSubstitute;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Tests.Controllers;

public class PublicGitSshKeyControllerTests : ControllerTestBase
{
    private const string SamplePublicKey = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQC7";

    public PublicGitSshKeyControllerTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory) { }

    [Fact]
    public async Task Create_ReturnsCreatedWithId()
    {
        await AuthenticateAsync("ssh-create-user", "ssh-create@example.com");

        var response = await Client.PostAsJsonAsync(
            "/public-git-ssh-key",
            CreateKeyQuery("Laptop")
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var createdId = await response.Content.ReadFromJsonAsync<PublicGitSshKeyId>();
        Assert.NotNull(createdId);
        Assert.NotEqual(Guid.Empty, createdId.Value);

        var getResponse = await Client.GetAsync($"/public-git-ssh-key/{createdId.Value}");
        getResponse.EnsureSuccessStatusCode();

        var dto = await getResponse.Content.ReadFromJsonAsync<PublicGitSshKeyDto>();
        Assert.NotNull(dto);
        Assert.Equal("Laptop", dto.Name);
        Assert.Equal(SamplePublicKey, dto.PublicSSHKey);
    }

    [Fact]
    public async Task Get_WhenMissing_ReturnsNotFound()
    {
        await AuthenticateAsync("ssh-get-missing", "ssh-get-missing@example.com");

        var response = await Client.GetAsync($"/public-git-ssh-key/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_WhenOwnedByOtherUser_ReturnsNotFound()
    {
        await AuthenticateAsync("ssh-owner", "ssh-owner@example.com");

        var createResponse = await Client.PostAsJsonAsync(
            "/public-git-ssh-key",
            CreateKeyQuery("Owner key")
        );
        createResponse.EnsureSuccessStatusCode();
        var createdId = await createResponse.Content.ReadFromJsonAsync<PublicGitSshKeyId>();
        Assert.NotNull(createdId);

        await AuthenticateAsync("ssh-intruder", "ssh-intruder@example.com");

        var response = await Client.GetAsync($"/public-git-ssh-key/{createdId.Value}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_WhenEmpty_ReturnsEmptyList()
    {
        await AuthenticateAsync("ssh-list-empty", "ssh-list-empty@example.com");

        var response = await Client.GetAsync("/public-git-ssh-key");
        response.EnsureSuccessStatusCode();

        var keys = await response.Content.ReadFromJsonAsync<List<PublicGitSshKeyDto>>();
        Assert.NotNull(keys);
        Assert.Empty(keys);
    }

    [Fact]
    public async Task List_ReturnsOnlyCurrentUsersKeys()
    {
        await AuthenticateAsync("ssh-list-owner", "ssh-list-owner@example.com");

        var createResponse = await Client.PostAsJsonAsync(
            "/public-git-ssh-key",
            CreateKeyQuery("Visible key")
        );
        createResponse.EnsureSuccessStatusCode();

        var listResponse = await Client.GetAsync("/public-git-ssh-key");
        listResponse.EnsureSuccessStatusCode();

        var keys = await listResponse.Content.ReadFromJsonAsync<List<PublicGitSshKeyDto>>();
        Assert.NotNull(keys);
        Assert.Single(keys);
        Assert.Equal("Visible key", keys[0].Name);

        await AuthenticateAsync("ssh-list-other", "ssh-list-other@example.com");

        var otherListResponse = await Client.GetAsync("/public-git-ssh-key");
        otherListResponse.EnsureSuccessStatusCode();

        var otherKeys = await otherListResponse.Content.ReadFromJsonAsync<
            List<PublicGitSshKeyDto>
        >();
        Assert.NotNull(otherKeys);
        Assert.Empty(otherKeys);
    }

    [Fact]
    public async Task Delete_WhenOwned_ReturnsNoContent()
    {
        await AuthenticateAsync("ssh-delete-owner", "ssh-delete-owner@example.com");

        var createResponse = await Client.PostAsJsonAsync(
            "/public-git-ssh-key",
            CreateKeyQuery("Delete me")
        );
        createResponse.EnsureSuccessStatusCode();
        var createdId = await createResponse.Content.ReadFromJsonAsync<PublicGitSshKeyId>();
        Assert.NotNull(createdId);

        var deleteResponse = await Client.DeleteAsync($"/public-git-ssh-key/{createdId.Value}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await Client.GetAsync($"/public-git-ssh-key/{createdId.Value}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenMissing_ReturnsNotFound()
    {
        await AuthenticateAsync("ssh-delete-missing", "ssh-delete-missing@example.com");

        var response = await Client.DeleteAsync($"/public-git-ssh-key/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenOwnedByOtherUser_ReturnsNotFound()
    {
        await AuthenticateAsync("ssh-delete-real-owner", "ssh-delete-real-owner@example.com");

        var createResponse = await Client.PostAsJsonAsync(
            "/public-git-ssh-key",
            CreateKeyQuery("Protected key")
        );
        createResponse.EnsureSuccessStatusCode();
        var createdId = await createResponse.Content.ReadFromJsonAsync<PublicGitSshKeyId>();
        Assert.NotNull(createdId);

        await AuthenticateAsync("ssh-delete-intruder", "ssh-delete-intruder@example.com");

        var response = await Client.DeleteAsync($"/public-git-ssh-key/{createdId.Value}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WhenQueryFails_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureUserLookup(queryProcessor, userId);
        queryProcessor
            .RunQueryAsync(Arg.Any<CreatePublicGitSshKeyQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<PublicGitSshKeyId>.None);

        var client = CreateAuthenticatedClient(queryProcessor, userId, "mock-create-user");

        var response = await client.PostAsJsonAsync("/public-git-ssh-key", CreateKeyQuery("Fails"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_WhenQueryReturnsNone_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureUserLookup(queryProcessor, userId);
        queryProcessor
            .RunQueryAsync(Arg.Any<GetPublicGitSshKeyQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<PublicGitSshKeyDto>.None);

        var client = CreateAuthenticatedClient(queryProcessor, userId, "mock-get-user");
        var response = await client.GetAsync($"/public-git-ssh-key/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_WhenQueryReturnsNone_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureUserLookup(queryProcessor, userId);
        queryProcessor
            .RunQueryAsync(Arg.Any<ListPublicGitSshKeyQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<IReadOnlyList<PublicGitSshKeyDto>>.None);

        var client = CreateAuthenticatedClient(queryProcessor, userId, "mock-list-user");
        var response = await client.GetAsync("/public-git-ssh-key");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenDeleteQueryFails_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var keyId = PublicGitSshKeyId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureUserLookup(queryProcessor, userId);
        queryProcessor
            .RunQueryAsync(Arg.Any<GetPublicGitSshKeyQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new PublicGitSshKeyDto
                    {
                        Id = keyId,
                        OwnerUserId = userId,
                        Name = "Existing",
                        PublicSSHKey = SamplePublicKey,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<DeletePublicGitSshKeyQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<Unit>.None);

        var client = CreateAuthenticatedClient(queryProcessor, userId, "mock-delete-user");
        var response = await client.DeleteAsync($"/public-git-ssh-key/{keyId.Value}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static CreatePublicGitSshKeyQuery CreateKeyQuery(string name) =>
        new()
        {
            ModelToCreate = new PublicGitSshKeyDto
            {
                // Satisfies request validation; the controller overwrites this from the JWT.
                OwnerUserId = UserId.From(Guid.NewGuid()),
                Name = name,
                PublicSSHKey = SamplePublicKey,
            },
        };

    private static void ConfigureUserLookup(IQueryProcessor queryProcessor, UserId userId)
    {
        queryProcessor
            .RunQueryAsync(Arg.Any<UserGetByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(new User { Id = userId, Username = "mockuser" }));
    }

    private async Task AuthenticateAsync(string username, string email)
    {
        var token = await RegisterUserAsync(username, email, "Password123!");
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private HttpClient CreateAuthenticatedClient(
        IQueryProcessor queryProcessor,
        UserId userId,
        string username
    )
    {
        var (client, _) = CreateClientWithQueryProcessor(queryProcessor);
        var token = JwtTokenGenerator.GetJWTToken(username, userId.Value.ToString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
