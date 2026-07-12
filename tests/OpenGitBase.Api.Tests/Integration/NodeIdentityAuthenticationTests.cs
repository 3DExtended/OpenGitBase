using Microsoft.EntityFrameworkCore;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.Entities;
using OpenGitBase.Features.ComputeNode.QueryHandlers;

namespace OpenGitBase.Api.Tests.Integration;

public class NodeIdentityAuthenticationTests
{
    [Fact]
    public async Task Register_ReturnsNodeIdentityToken_WithHashedStorage()
    {
        await using var scope = await OrgComputeTestScope.CreateAsync();
        var enrollment = await scope.CreateEnrollmentHandler.RunQueryAsync(
            new CreateComputeNodeEnrollmentQuery
            {
                NodeId = "auth-node-1",
                CreatedByUserId = Guid.NewGuid(),
                MaxConcurrentJobs = 1,
                MaxCpu = 1,
                MaxMemoryBytes = 1024,
            },
            CancellationToken.None
        );

        var register = await scope.RegisterHandler.RunQueryAsync(
            new RegisterComputeNodeQuery
            {
                NodeId = "auth-node-1",
                EnrollmentToken = enrollment.Get().EnrollmentToken,
            },
            CancellationToken.None
        );

        Assert.True(register.IsSome);
        Assert.False(string.IsNullOrWhiteSpace(register.Get().NodeIdentityToken));
        Assert.True(ComputeNodeIdentityTokens.TryParseNodeId(register.Get().NodeIdentityToken, out _));

        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        var entity = await context
            .Set<ComputeNodeEntity>()
            .FirstAsync(node => node.NodeId == "auth-node-1");
        Assert.False(string.IsNullOrWhiteSpace(entity.IdentityTokenHash));
    }

    [Fact]
    public async Task AuthenticateAsync_ValidToken_ReturnsNode_InvalidToken_ReturnsNull()
    {
        await using var scope = await OrgComputeTestScope.CreateAsync();
        var hasher = new PasswordHasherService();
        var enrollment = await scope.CreateEnrollmentHandler.RunQueryAsync(
            new CreateComputeNodeEnrollmentQuery
            {
                NodeId = "auth-node-2",
                CreatedByUserId = Guid.NewGuid(),
                MaxConcurrentJobs = 1,
                MaxCpu = 1,
                MaxMemoryBytes = 1024,
            },
            CancellationToken.None
        );
        var register = await scope.RegisterHandler.RunQueryAsync(
            new RegisterComputeNodeQuery
            {
                NodeId = "auth-node-2",
                EnrollmentToken = enrollment.Get().EnrollmentToken,
            },
            CancellationToken.None
        );
        var token = register.Get().NodeIdentityToken;
        var service = new ComputeNodeIdentityService(scope.ContextFactory, hasher);

        var node = await service.AuthenticateAsync(token, CancellationToken.None);
        Assert.NotNull(node);
        Assert.Equal("auth-node-2", node!.NodeId);

        var invalid = await service.AuthenticateAsync("not-a-valid-token", CancellationToken.None);
        Assert.Null(invalid);
    }
}
