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

namespace OpenGitBase.Features.Organization.Tests.QueryHandlers;

public class DeclineOrganizationInviteQueryHandlerTests
{
    private static readonly EncryptionOptions EncryptionOptions =
        new() { DataKey = Convert.ToBase64String(new byte[32]), Pepper = "org-test-pepper" };

    [Fact]
    public async Task RunQueryAsync_WhenInviteValid_DeclinesInvite()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(DeclineOrganizationInviteQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var passwordHasher = new PasswordHasherService();
        var emailProtection = new EmailProtectionService(EncryptionOptions);
        var token = "decline-token";
        await using var seedContext = await scope.CreateDbContextAsync();
        var (organizationId, _, ownerUserId) = await OrganizationTestData.SeedAsync(seedContext);
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

        var handler = new DeclineOrganizationInviteQueryHandler(
            scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>(),
            passwordHasher,
            new FixedSystemClock(DateTimeOffset.UtcNow)
        );
        var result = await handler.RunQueryAsync(
            new DeclineOrganizationInviteQuery { Token = token },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);
    }
}
