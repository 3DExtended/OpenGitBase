using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.Tests.Testing;

public static class PipelineTestData
{
    public const string SampleName = "Sample";
    public const string UpdatedName = "Updated";

    public static async Task<(PipelineId Id, PipelineEntity Entity)> SeedAsync(
        OpenGitBaseDbContext context
    )
    {
        var id = Guid.NewGuid();
        var entity = new PipelineEntity { Id = id, Name = SampleName };
        context.Set<PipelineEntity>().Add(entity);
        await context.SaveChangesAsync().ConfigureAwait(false);
        return (PipelineId.From(id), entity);
    }
}
