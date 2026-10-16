using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Pipeline;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

/// <summary>
/// The scheduler drops an invalid CI file silently, so this repository's own file is checked
/// here: a push to main must start its test jobs.
/// </summary>
public sealed class RepositoryCiFileTests
{
    private const string CommitSha = "3f786850e387550fdab836ed7e6dc881de23001b";

    private static readonly string CiYaml = File.ReadAllText(
        Path.Combine(FindRepositoryRoot(), ".opengitbase-ci.yml")
    );

    [Fact]
    public async Task APushToMain_QueuesTheStorageAndDotnetTestsOnHostedCompute()
    {
        await using var scope = await PipelineHandlerTestScope.CreateAsync();

        await ScheduleAsync(scope, "refs/heads/main");

        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        var jobs = await context.Set<PipelineJobEntity>().OrderBy(job => job.Name).ToListAsync();
        Assert.Equal(["dotnet-tests", "storage-tests"], jobs.Select(job => job.Name));
        Assert.All(jobs, job => Assert.Equal(PipelineJobStatus.Queued, job.Status));
        Assert.All(jobs, job => Assert.Equal("ogb-hosted", job.RunsOn));
    }

    [Fact]
    public async Task APushToAnotherBranch_SchedulesNoRun()
    {
        await using var scope = await PipelineHandlerTestScope.CreateAsync();

        await ScheduleAsync(scope, "refs/heads/feature");

        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.Set<PipelineRunEntity>().AnyAsync());
    }

    // The agent refuses a script that names a host missing from the egress allowlist, which
    // starts out empty; the toolchains come from the image's package archive instead.
    [Fact]
    public async Task NoJobNamesADownloadUrl()
    {
        await using var scope = await PipelineHandlerTestScope.CreateAsync();

        await ScheduleAsync(scope, "refs/heads/main");

        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        foreach (var job in await context.Set<PipelineJobEntity>().ToListAsync())
        {
            var spec = JsonSerializer.Deserialize<ResolvedJob>(job.ResolvedSpecJson)!;
            var scripts = spec.Dependencies.Select(dependency => dependency.InstallScript ?? string.Empty)
                .Append(spec.Script);
            Assert.DoesNotContain(scripts, script => script.Contains("://", StringComparison.Ordinal));
        }
    }

    private static async Task ScheduleAsync(PipelineHandlerTestScope scope, string pushedRef)
    {
        var repositoryId = Guid.NewGuid();
        await using (var context = await scope.ContextFactory.CreateDbContextAsync())
        {
            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "open-git-base",
                    OwnerUserId = Guid.NewGuid(),
                    Slug = "open-git-base",
                    PhysicalPath = "/srv/git/open-git-base.git",
                }
            );
            await context.SaveChangesAsync();
        }

        var handler = new SchedulePipelineRunFromPushQueryHandler(
            scope.ContextFactory,
            scope.Mapper,
            scope.JobPublisher,
            FakeRepositoryFileReader.WithFiles(new Dictionary<string, string> { [".opengitbase-ci.yml"] = CiYaml })
        );
        await handler.RunQueryAsync(
            new SchedulePipelineRunFromPushQuery
            {
                RepositoryId = repositoryId,
                Ref = pushedRef,
                AfterSha = CommitSha,
            },
            CancellationToken.None
        );
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenGitBase.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("OpenGitBase.sln not found above the test assembly.");
    }
}
