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

public class RevokeOrganizationInviteQueryHandlerTests
{
    private static readonly EncryptionOptions EncryptionOptions =
        new() { DataKey = Convert.ToBase64String(new byte[32]), Pepper = "org-test-pepper" };

    [Fact]
    public async Task RunQueryAsync_WhenInviteExists_RevokesInvite()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(RevokeOrganizationInviteQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var emailProtection = new EmailProtectionService(EncryptionOptions);
        await using var seedContext = await scope.CreateDbContextAsync();
        var (organizationId, _, ownerUserId) = await OrganizationTestData.SeedAsync(seedContext);
        var inviteId = Guid.NewGuid();
        seedContext.Set<OrganizationInviteEntity>().Add(
            new OrganizationInviteEntity
            {
                Id = inviteId,
                OrganizationId = organizationId.Value,
                EmailLookupHash = emailProtection.ComputeLookupHash("invitee@example.com"),
                EmailCiphertext = emailProtection.EncryptEmail("invitee@example.com"),
                Role = OrganizationMemberRole.Member,
                TokenHash = "hash",
                InvitedByUserId = ownerUserId.Value,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                Status = OrganizationInviteStatus.Pending,
            }
        );
        await seedContext.SaveChangesAsync();

        var handler = scope.GetHandler<RevokeOrganizationInviteQueryHandler>();
        var result = await handler.RunQueryAsync(
            new RevokeOrganizationInviteQuery
            {
                OrganizationId = organizationId,
                InviteId = OrganizationInviteId.From(inviteId),
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);
    }
}
