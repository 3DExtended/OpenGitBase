using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Organization.QueryHandlers;
using OpenGitBase.Features.Organization.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Organization.Tests.QueryHandlers;

public class AcceptOrganizationInviteQueryHandlerTests
{
    private static readonly EncryptionOptions EncryptionOptions =
        new() { DataKey = Convert.ToBase64String(new byte[32]), Pepper = "org-test-pepper" };

    [Fact]
    public async Task RunQueryAsync_WhenInviteValid_AddsMember()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(AcceptOrganizationInviteQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var emailProtection = new EmailProtectionService(EncryptionOptions);
        var passwordHasher = new PasswordHasherService();
        var token = "accept-token";
        var userId = UserId.From(Guid.NewGuid());

        await using var seedContext = await scope.CreateDbContextAsync();
        var (organizationId, _, ownerUserId) = await OrganizationTestData.SeedAsync(seedContext);
        seedContext.Set<UserEntity>().Add(OrganizationTestData.CreateUser(userId.Value, "invitee"));
        seedContext.Set<UserCredentialsEntity>().Add(
            new UserCredentialsEntity
            {
                UserId = userId.Value,
                Username = "invitee",
                SignInProvider = false,
                EmailCiphertext = emailProtection.EncryptEmail("invitee@example.com"),
                EmailLookupHash = emailProtection.ComputeLookupHash("invitee@example.com"),
            }
        );
        seedContext.Set<OrganizationInviteEntity>().Add(
            new OrganizationInviteEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId.Value,
                EmailLookupHash = emailProtection.ComputeLookupHash("invitee@example.com"),
                EmailCiphertext = emailProtection.EncryptEmail("invitee@example.com"),
                Role = OrganizationMemberRole.Member,
                TokenHash = passwordHasher.HashPassword(token),
                InvitedByUserId = ownerUserId.Value,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                Status = OrganizationInviteStatus.Pending,
            }
        );
        await seedContext.SaveChangesAsync();

        var handler = new AcceptOrganizationInviteQueryHandler(
            scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>(),
            passwordHasher,
            new FixedSystemClock(DateTimeOffset.UtcNow)
        );
        var result = await handler.RunQueryAsync(
            new AcceptOrganizationInviteQuery
            {
                Token = token,
                UserId = userId,
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            value => Assert.Equal(AcceptOrganizationInviteResult.Accepted, value)
        );
    }
}
