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

public class ListOrganizationInvitesQueryHandlerTests
{
    private static readonly EncryptionOptions EncryptionOptions =
        new() { DataKey = Convert.ToBase64String(new byte[32]), Pepper = "org-test-pepper" };

    [Fact]
    public async Task RunQueryAsync_WhenMemberView_RedactsEmail()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(ListOrganizationInvitesQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var emailProtection = new EmailProtectionService(EncryptionOptions);
        var now = DateTimeOffset.UtcNow;
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
                TokenHash = "ignored",
                InvitedByUserId = ownerUserId.Value,
                CreatedAt = now,
                ExpiresAt = now.AddDays(7),
                Status = OrganizationInviteStatus.Pending,
            }
        );
        await seedContext.SaveChangesAsync();

        var handler = new ListOrganizationInvitesQueryHandler(
            scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>(),
            emailProtection,
            new FixedSystemClock(now)
        );
        var result = await handler.RunQueryAsync(
            new ListOrganizationInvitesQuery
            {
                OrganizationId = organizationId,
                RevealEmail = false,
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            invites => Assert.Contains("***", Assert.Single(invites).Email, StringComparison.Ordinal)
        );
    }
}
