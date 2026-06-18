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

public class GetOrganizationInviteByTokenQueryHandlerTests
{
    private static readonly EncryptionOptions EncryptionOptions =
        new() { DataKey = Convert.ToBase64String(new byte[32]), Pepper = "org-test-pepper" };

    [Fact]
    public async Task RunQueryAsync_WhenTokenMatches_ReturnsPublicInvite()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(GetOrganizationInviteByTokenQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var emailProtection = new EmailProtectionService(EncryptionOptions);
        var passwordHasher = new PasswordHasherService();
        var token = "token-abc";
        await using var seedContext = await scope.CreateDbContextAsync();
        var (organizationId, _, ownerUserId) = await OrganizationTestData.SeedAsync(seedContext);
        seedContext.Set<OrganizationInviteEntity>().Add(
            new OrganizationInviteEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId.Value,
                EmailLookupHash = emailProtection.ComputeLookupHash("person@example.com"),
                EmailCiphertext = emailProtection.EncryptEmail("person@example.com"),
                Role = OrganizationMemberRole.Member,
                TokenHash = passwordHasher.HashPassword(token),
                InvitedByUserId = ownerUserId.Value,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                Status = OrganizationInviteStatus.Pending,
            }
        );
        await seedContext.SaveChangesAsync();

        var handler = new GetOrganizationInviteByTokenQueryHandler(
            scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>(),
            emailProtection,
            passwordHasher,
            new FixedSystemClock(DateTimeOffset.UtcNow)
        );
        var result = await handler.RunQueryAsync(
            new GetOrganizationInviteByTokenQuery { Token = token },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            value =>
            {
                Assert.Equal(OrganizationTestData.SampleSlug, value.OrganizationSlug);
                Assert.Equal(OrganizationInviteStatus.Pending, value.Status);
            }
        );
    }
}
