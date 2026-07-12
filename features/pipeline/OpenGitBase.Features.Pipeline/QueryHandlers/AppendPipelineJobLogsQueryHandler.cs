using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class AppendPipelineJobLogsQueryHandler : IQueryHandler<AppendPipelineJobLogsQuery, bool>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public AppendPipelineJobLogsQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<bool>> RunQueryAsync(
        AppendPipelineJobLogsQuery query,
        CancellationToken cancellationToken
    )
    {
        var normalizedLogLines = query
            .LogLines.Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.TrimEnd('\r', '\n'))
            .Where(line => line.Length > 0)
            .ToList();
        if (normalizedLogLines.Count == 0)
        {
            return Option.From(true);
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var jobExists = await context
            .Set<PipelineJobEntity>()
            .AnyAsync(entity => entity.Id == query.JobId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (!jobExists)
        {
            return Option<bool>.None;
        }

        var section = string.IsNullOrWhiteSpace(query.LogSection) ? "script" : query.LogSection.Trim();
        var timestamp = DateTimeOffset.UtcNow;
        foreach (var line in normalizedLogLines)
        {
            context.Set<PipelineJobLogEntity>()
                .Add(
                    new PipelineJobLogEntity
                    {
                        Id = Guid.NewGuid(),
                        JobId = query.JobId.Value,
                        Section = section,
                        Line = line.Length <= 4000 ? line : line[..4000],
                        Timestamp = timestamp,
                    }
                );
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(true);
    }
}
