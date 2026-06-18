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

public class CreateOrRefreshOrganizationInviteQueryHandlerTests
{
    private static readonly EncryptionOptions EncryptionOptions =
        new() { DataKey = Convert.ToBase64String(new byte[32]), Pepper = "org-test-pepper" };

    [Fact]
    public async Task RunQueryAsync_WhenNoPendingInvite_CreatesInvite()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(CreateOrRefreshOrganizationInviteQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (organizationId, _, ownerUserId) = await OrganizationTestData.SeedAsync(seedContext);
        var handler = new CreateOrRefreshOrganizationInviteQueryHandler(
            scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>(),
            new EmailProtectionService(EncryptionOptions),
            new PasswordHasherService(),
            new FixedSystemClock(DateTimeOffset.UtcNow)
        );

        var result = await handler.RunQueryAsync(
            new CreateOrRefreshOrganizationInviteQuery
            {
                OrganizationId = organizationId,
                Email = "invitee@example.com",
                Role = OrganizationMemberRole.Member,
                InvitedByUserId = ownerUserId.Value,
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(result, value => Assert.NotEmpty(value.Token));
        await using var verifyContext = await scope.CreateDbContextAsync();
        var invite = await verifyContext
            .Set<OrganizationInviteEntity>()
            .SingleAsync(x => x.OrganizationId == organizationId.Value);
        Assert.Equal(OrganizationInviteStatus.Pending, invite.Status);
    }
}
