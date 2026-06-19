using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Features.GitAccessToken.Contracts;

namespace OpenGitBase.Api.Tests.Controllers;

public class GitAccessTokenControllerTests : ControllerTestBase
{
    public GitAccessTokenControllerTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory) { }

    [Fact]
    public async Task Create_ReturnsTokenOnce()
    {
        await AuthenticateAsync("pat-create-user", "pat-create@example.com");

        var response = await Client.PostAsJsonAsync(
            "/git-access-token",
            new CreateGitAccessTokenRequest
            {
                Name = "Laptop",
                Scope = GitAccessTokenScopes.Write,
                NeverExpires = true,
            }
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateGitAccessTokenResult>();
        Assert.NotNull(created);
        Assert.StartsWith("ogb_", created.Token, StringComparison.Ordinal);
        Assert.Equal("Laptop", created.Metadata.Name);
        Assert.Null(created.Metadata.ExpiresAt);
    }

    [Fact]
    public async Task List_ReturnsOnlyCurrentUsersTokens()
    {
        await AuthenticateAsync("pat-list-owner", "pat-list-owner@example.com");

        var createResponse = await Client.PostAsJsonAsync(
            "/git-access-token",
            new CreateGitAccessTokenRequest { Name = "Visible", Scope = GitAccessTokenScopes.Read }
        );
        createResponse.EnsureSuccessStatusCode();

        await AuthenticateAsync("pat-list-other", "pat-list-other@example.com");

        var listResponse = await Client.GetAsync("/git-access-token");
        listResponse.EnsureSuccessStatusCode();

        var tokens = await listResponse.Content.ReadFromJsonAsync<List<GitAccessTokenDto>>();
        Assert.NotNull(tokens);
        Assert.Empty(tokens);
    }

    [Fact]
    public async Task Delete_RevokesToken()
    {
        await AuthenticateAsync("pat-delete-user", "pat-delete@example.com");

        var createResponse = await Client.PostAsJsonAsync(
            "/git-access-token",
            new CreateGitAccessTokenRequest { Name = "Revoke me", Scope = GitAccessTokenScopes.Read }
        );
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateGitAccessTokenResult>();
        Assert.NotNull(created);

        var deleteResponse = await Client.DeleteAsync($"/git-access-token/{created.Id.Value}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await Client.GetAsync($"/git-access-token/{created.Id.Value}");
        getResponse.EnsureSuccessStatusCode();
        var dto = await getResponse.Content.ReadFromJsonAsync<GitAccessTokenDto>();
        Assert.NotNull(dto);
        Assert.NotNull(dto.RevokedAt);
    }

    private async Task AuthenticateAsync(string username, string email)
    {
        var token = await RegisterUserAsync(username, email, "Password123!");
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
