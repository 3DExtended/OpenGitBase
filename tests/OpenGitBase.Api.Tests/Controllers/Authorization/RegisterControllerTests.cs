using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Tests.Controllers.Authorization;

public class RegisterControllerTests : ControllerTestBase
{
    public RegisterControllerTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory) { }

    [Fact]
    public async Task Register_Success_SetsAuthCookie()
    {
        var response = await Client.PostAsJsonAsync(
            "/register/register",
            new RegisterDto
            {
                Username = "cookie-register",
                Email = "cookie-register@example.com",
                Password = "Password123!",
            }
        );

        response.EnsureSuccessStatusCode();
        Assert.Contains(
            AuthCookieOptions.CookieName,
            response.Headers.GetValues("Set-Cookie").First()
        );
    }

    [Fact]
    public async Task Register_Success_ReturnsJwt()
    {
        var token = await RegisterUserAsync("newuser", "new@example.com", "Password123!");
        await VerifyJwtAsync(token);
    }

    [Fact]
    public async Task Register_WithNullBody_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync<RegisterDto?>("/register/register", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("", "test@example.com", "Password123!")]
    [InlineData("user", "", "Password123!")]
    [InlineData("user", "test@example.com", "")]
    public async Task Register_WithMissingFields_ReturnsBadRequest(
        string username,
        string email,
        string password
    )
    {
        var response = await Client.PostAsJsonAsync(
            "/register/register",
            new RegisterDto
            {
                Username = username,
                Email = email,
                Password = password,
            }
        );
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithTakenUsername_ReturnsConflict()
    {
        await RegisterUserAsync("takenuser", "first@example.com", "Password123!");

        var response = await Client.PostAsJsonAsync(
            "/register/register",
            new RegisterDto
            {
                Username = "takenuser",
                Email = "second@example.com",
                Password = "Password123!",
            }
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Username taken", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_WithTakenEmail_ReturnsConflict()
    {
        await RegisterUserAsync("firstuser", "shared@example.com", "Password123!");

        var response = await Client.PostAsJsonAsync(
            "/register/register",
            new RegisterDto
            {
                Username = "seconduser",
                Email = "shared@example.com",
                Password = "Password123!",
            }
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Email taken", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_WithReservedUsername_ReturnsConflict()
    {
        var response = await Client.PostAsJsonAsync(
            "/register/register",
            new RegisterDto
            {
                Username = "settings",
                Email = "settings@example.com",
                Password = "Password123!",
            }
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Reserved username", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_WhenRegisterQueryFails_ReturnsBadRequest()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<UserExistsByUsernameQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<UserId>.None);
        queryProcessor
            .RunQueryAsync(Arg.Any<UserExistsByEmailQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(false));
        queryProcessor
            .RunQueryAsync(Arg.Any<UserRegisterQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<UserId>.None);

        var (client, _) = CreateClientWithQueryProcessor(queryProcessor);

        var response = await client.PostAsJsonAsync(
            "/register/register",
            new RegisterDto
            {
                Username = "failuser",
                Email = "fail@example.com",
                Password = "Password123!",
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OpenProvider_Success_ReturnsJwt()
    {
        const string registrationToken = "open-provider-token";
        const string internalId = "apple-provider-subject";
        SeedRegistrationCache(registrationToken, internalId, "provider@example.com");

        var response = await Client.PostAsJsonAsync(
            "/register/openprovider",
            new OpenProviderRegisterDto
            {
                Username = "provideruser",
                RegistrationToken = registrationToken,
            }
        );

        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadAsStringAsync();
        await VerifyJwtAsync(token);
    }

    [Fact]
    public async Task OpenProvider_WithNullBody_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync<OpenProviderRegisterDto?>(
            "/register/openprovider",
            null
        );
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("", "token")]
    [InlineData("user", "")]
    public async Task OpenProvider_WithMissingFields_ReturnsBadRequest(
        string username,
        string token
    )
    {
        var response = await Client.PostAsJsonAsync(
            "/register/openprovider",
            new OpenProviderRegisterDto { Username = username, RegistrationToken = token }
        );
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OpenProvider_WithInvalidToken_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync(
            "/register/openprovider",
            new OpenProviderRegisterDto
            {
                Username = "provideruser",
                RegistrationToken = "missing-token",
            }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OpenProvider_WithIncompleteCache_ReturnsUnauthorized()
    {
        SeedRegistrationCacheEntry(
            "incomplete",
            new Dictionary<string, string> { { "sub", "provider-subject" } }
        );

        var response = await Client.PostAsJsonAsync(
            "/register/openprovider",
            new OpenProviderRegisterDto
            {
                Username = "provideruser",
                RegistrationToken = "incomplete",
            }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OpenProvider_WithTakenUsername_ReturnsConflict()
    {
        await RegisterUserAsync("existinguser", "existing@example.com", "Password123!");

        const string registrationToken = "username-conflict-token";
        SeedRegistrationCache(registrationToken, "provider-subject", "provider@example.com");

        var response = await Client.PostAsJsonAsync(
            "/register/openprovider",
            new OpenProviderRegisterDto
            {
                Username = "existinguser",
                RegistrationToken = registrationToken,
            }
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Username taken", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OpenProvider_WithTakenEmail_ReturnsConflict()
    {
        await RegisterUserAsync("firstprovider", "provider@example.com", "Password123!");

        const string registrationToken = "email-conflict-token";
        SeedRegistrationCache(
            registrationToken,
            "another-provider-subject",
            "provider@example.com"
        );

        var response = await Client.PostAsJsonAsync(
            "/register/openprovider",
            new OpenProviderRegisterDto
            {
                Username = "secondprovider",
                RegistrationToken = registrationToken,
            }
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Email taken", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OpenProvider_WhenCreateFails_ReturnsBadRequest()
    {
        const string registrationToken = "create-fail-token";
        SeedRegistrationCache(registrationToken, "create-fail-subject", "createfail@example.com");

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<UserExistsByUsernameQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<UserId>.None);
        queryProcessor
            .RunQueryAsync(Arg.Any<UserExistsByEmailQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(false));
        queryProcessor
            .RunQueryAsync(Arg.Any<UserCreateQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<UserId>.None);

        var (client, cache) = CreateClientWithQueryProcessor(queryProcessor);
        cache.Set(
            "registrationapikey" + registrationToken,
            new Dictionary<string, string>
            {
                { "sub", "create-fail-subject" },
                { "email", "createfail@example.com" },
            },
            TimeSpan.FromHours(2)
        );

        var response = await client.PostAsJsonAsync(
            "/register/openprovider",
            new OpenProviderRegisterDto
            {
                Username = "createfailuser",
                RegistrationToken = registrationToken,
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
