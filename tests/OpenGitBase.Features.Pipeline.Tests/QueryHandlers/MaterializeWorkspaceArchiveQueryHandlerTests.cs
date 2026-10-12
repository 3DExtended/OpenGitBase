using System.Diagnostics;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public sealed class MaterializeWorkspaceArchiveQueryHandlerTests : IDisposable
{
    private readonly string _workDir = Path.Combine(
        Path.GetTempPath(),
        $"ogb-ws-test-{Guid.NewGuid():N}"
    );

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task RunQueryAsync_WithCommitSha_ReturnsWorkspaceArchive(int gitDepth)
    {
        var (repositoryPath, commitSha) = await CreateRepositoryWithCommitAsync();
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var (handler, query) = await SeedJobAsync(scope, repositoryPath, commitSha, gitDepth);

        var result = await handler.RunQueryAsync(query, CancellationToken.None);

        Assert.True(result.IsSome);
        Assert.NotEmpty(result.Get().ArchiveBytes);
        Assert.Equal(gitDepth > 0, result.Get().IsShallow);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task RunQueryAsync_WhenAfterShaIsNotAnObjectId_ReturnsNone(int gitDepth)
    {
        var (repositoryPath, _) = await CreateRepositoryWithCommitAsync();
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var (handler, query) = await SeedJobAsync(scope, repositoryPath, "HEAD", gitDepth);

        var result = await handler.RunQueryAsync(query, CancellationToken.None);

        Assert.True(result.IsNone);
    }

    private static async Task<(MaterializeWorkspaceArchiveQueryHandler Handler, MaterializeWorkspaceArchiveQuery Query)> SeedJobAsync(
        PipelineHandlerTestScope scope,
        string repositoryPath,
        string afterSha,
        int gitDepth
    )
    {
        var hasher = new PasswordHasherService();
        var repositoryId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var token = JobIdentityTokens.Mint(jobId);

        await using (var context = await scope.ContextFactory.CreateDbContextAsync())
        {
            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "repo",
                    OwnerUserId = Guid.NewGuid(),
                    Slug = "repo",
                    PhysicalPath = repositoryPath,
                }
            );
            context.Set<PipelineRunEntity>().Add(
                new PipelineRunEntity
                {
                    Id = runId,
                    RepositoryId = repositoryId,
                    Ref = "main",
                    AfterSha = afterSha,
                    Status = PipelineRunStatus.Running,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
            context.Set<PipelineJobEntity>().Add(
                new PipelineJobEntity
                {
                    Id = jobId,
                    RunId = runId,
                    Name = "build",
                    Stage = "build",
                    RunsOn = "ogb-hosted",
                    Status = PipelineJobStatus.Running,
                    Script = "echo ok",
                    GitDepth = gitDepth,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
            context.Set<JobIdentityEntity>().Add(
                new JobIdentityEntity
                {
                    Id = Guid.NewGuid(),
                    JobId = jobId,
                    TokenHash = hasher.HashPassword(token),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                }
            );
            await context.SaveChangesAsync();
        }

        var handler = new MaterializeWorkspaceArchiveQueryHandler(scope.ContextFactory, hasher);
        var query = new MaterializeWorkspaceArchiveQuery
        {
            JobId = PipelineJobId.From(jobId),
            JobIdentityToken = token,
        };
        return (handler, query);
    }

    private static async Task<string> RunGitAsync(string workingDirectory, params string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = workingDirectory,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(
            process.ExitCode == 0,
            $"git {string.Join(' ', arguments)} failed: {await stderr}"
        );
        return await stdout;
    }

    private async Task<(string RepositoryPath, string CommitSha)> CreateRepositoryWithCommitAsync()
    {
        var sourcePath = Path.Combine(_workDir, "source");
        var repositoryPath = Path.Combine(_workDir, "repo.git");
        Directory.CreateDirectory(_workDir);

        await RunGitAsync(_workDir, "init", "-b", "main", sourcePath);
        await File.WriteAllTextAsync(Path.Combine(sourcePath, "README.md"), "workspace\n");
        await RunGitAsync(sourcePath, "add", "README.md");
        await RunGitAsync(
            sourcePath,
            "-c",
            "user.name=OGB Test",
            "-c",
            "user.email=test@example.com",
            "-c",
            "commit.gpgsign=false",
            "commit",
            "-m",
            "init"
        );
        await RunGitAsync(_workDir, "clone", "--bare", sourcePath, repositoryPath);
        var commitSha = (await RunGitAsync(repositoryPath, "rev-parse", "HEAD")).Trim();
        return (repositoryPath, commitSha);
    }
}
