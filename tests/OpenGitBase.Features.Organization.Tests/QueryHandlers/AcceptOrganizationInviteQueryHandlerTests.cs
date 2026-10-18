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

    [Fact]
    public async Task RunQueryAsync_AcceptsTheInviteOnceWhenTheCommitAcknowledgementIsLost()
    {
        var lostCommit = new LostCommitSimulator();
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            lostCommit.Configure,
            typeof(AcceptOrganizationInviteQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var passwordHasher = new PasswordHasherService();
        var token = "accept-token";
        var userId = UserId.From(Guid.NewGuid());
        Guid inviteId;
        await using (var seedContext = await scope.CreateDbContextAsync())
        {
            inviteId = await SeedPendingInviteAsync(seedContext, passwordHasher, token, userId);
        }

        lostCommit.LoseNextCommitInserting("OrganizationMember");
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

        Assert.Equal(1, lostCommit.LostCommits);
        QueryHandlerResultAssert.AssertSome(
            result,
            value => Assert.Equal(AcceptOrganizationInviteResult.Accepted, value)
        );
        await using var verifyContext = await scope.CreateDbContextAsync();
        var member = Assert.Single(
            await verifyContext.Set<OrganizationMemberEntity>().Where(x => x.UserId == userId.Value).ToListAsync()
        );
        Assert.Equal(OrganizationMemberRole.Member, member.Role);
        var invite = await verifyContext.Set<OrganizationInviteEntity>().SingleAsync(x => x.Id == inviteId);
        Assert.Equal(OrganizationInviteStatus.Accepted, invite.Status);
    }

    private static async Task<Guid> SeedPendingInviteAsync(
        OpenGitBaseDbContext context,
        PasswordHasherService passwordHasher,
        string token,
        UserId userId
    )
    {
        var emailProtection = new EmailProtectionService(EncryptionOptions);
        var (organizationId, _, ownerUserId) = await OrganizationTestData.SeedAsync(context);
        context.Set<UserEntity>().Add(OrganizationTestData.CreateUser(userId.Value, "invitee"));
        context.Set<UserCredentialsEntity>().Add(
            new UserCredentialsEntity
            {
                UserId = userId.Value,
                Username = "invitee",
                SignInProvider = false,
                EmailCiphertext = emailProtection.EncryptEmail("invitee@example.com"),
                EmailLookupHash = emailProtection.ComputeLookupHash("invitee@example.com"),
            }
        );
        var inviteId = Guid.NewGuid();
        context.Set<OrganizationInviteEntity>().Add(
            new OrganizationInviteEntity
            {
                Id = inviteId,
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
        await context.SaveChangesAsync();
        return inviteId;
    }
}
