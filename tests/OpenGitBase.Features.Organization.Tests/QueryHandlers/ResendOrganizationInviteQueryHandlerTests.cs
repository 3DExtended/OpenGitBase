using Microsoft.EntityFrameworkCore;
using NSubstitute;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Organization.QueryHandlers;
using OpenGitBase.Features.Organization.Tests.Testing;

namespace OpenGitBase.Features.Organization.Tests.QueryHandlers;

public class ResendOrganizationInviteQueryHandlerTests
{
    private static readonly EncryptionOptions EncryptionOptions =
        new() { DataKey = Convert.ToBase64String(new byte[32]), Pepper = "org-test-pepper" };

    [Fact]
    public async Task RunQueryAsync_WhenPendingInviteExists_ResendsEmail()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(ResendOrganizationInviteQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var emailProtection = new EmailProtectionService(EncryptionOptions);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<EmailSendQuery>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

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

        var handler = new ResendOrganizationInviteQueryHandler(
            scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>(),
            emailProtection,
            new PasswordHasherService(),
            queryProcessor,
            new FixedSystemClock(DateTimeOffset.UtcNow)
        );
        var result = await handler.RunQueryAsync(
            new ResendOrganizationInviteQuery
            {
                OrganizationId = organizationId,
                InviteId = OrganizationInviteId.From(inviteId),
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);
        await queryProcessor
            .Received(1)
            .RunQueryAsync(Arg.Any<EmailSendQuery>(), Arg.Any<CancellationToken>());
    }
}
