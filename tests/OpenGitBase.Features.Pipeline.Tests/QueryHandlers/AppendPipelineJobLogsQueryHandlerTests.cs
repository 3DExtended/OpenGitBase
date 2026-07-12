using Microsoft.EntityFrameworkCore;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public class AppendPipelineJobLogsQueryHandlerTests
{
    [Fact]
    public async Task AppendJobLogs_PersistsIncrementalLines()
    {
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var jobId = Guid.NewGuid();
        await using (var context = scope.ContextFactory.CreateDbContext())
        {
            context.Set<PipelineJobEntity>()
                .Add(
                    new PipelineJobEntity
                    {
                        Id = jobId,
                        RunId = Guid.NewGuid(),
                        Name = "build",
                        Stage = "build",
                        RunsOn = "ogb-hosted",
                        Status = PipelineJobStatus.Running,
                        Script = "echo hi",
                        CreatedAt = DateTimeOffset.UtcNow,
                    }
                );
            await context.SaveChangesAsync();
        }

        var handler = new AppendPipelineJobLogsQueryHandler(scope.ContextFactory);
        var result = await handler.RunQueryAsync(
            new AppendPipelineJobLogsQuery
            {
                JobId = PipelineJobId.From(jobId),
                LogSection = "script",
                LogLines = ["line-one", "line-two"],
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        await using (var context = scope.ContextFactory.CreateDbContext())
        {
            var logs = await context
                .Set<PipelineJobLogEntity>()
                .Where(entity => entity.JobId == jobId)
                .OrderBy(entity => entity.Line)
                .Select(entity => entity.Line)
                .ToListAsync();
            Assert.Equal(["line-one", "line-two"], logs);
        }
    }
}
