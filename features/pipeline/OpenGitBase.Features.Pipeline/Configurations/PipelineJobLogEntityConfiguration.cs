using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.Configurations;

public sealed class PipelineJobLogEntityConfiguration : IEntityTypeConfiguration<PipelineJobLogEntity>
{
    public void Configure(EntityTypeBuilder<PipelineJobLogEntity> builder)
    {
        builder.ToTable("PipelineJobLog");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Section).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Line).HasMaxLength(4000).IsRequired();
        builder.HasIndex(entity => new { entity.JobId, entity.Timestamp });
    }
}
