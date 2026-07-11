using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.Configurations;

public class PipelineEntityConfiguration : IEntityTypeConfiguration<Entities.PipelineEntity>
{
    public void Configure(EntityTypeBuilder<Entities.PipelineEntity> builder)
    {
        builder.ToTable("Pipeline");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(256).IsRequired();
    }
}
