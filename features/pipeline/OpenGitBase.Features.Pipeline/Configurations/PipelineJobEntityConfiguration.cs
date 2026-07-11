using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.Configurations;

public sealed class PipelineJobEntityConfiguration : IEntityTypeConfiguration<PipelineJobEntity>
{
    public void Configure(EntityTypeBuilder<PipelineJobEntity> builder)
    {
        builder.ToTable("PipelineJob");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.Stage).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.RunsOn).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Script).HasColumnType("text").IsRequired();
        builder.Property(entity => entity.ResolvedSpecJson).HasColumnType("text").IsRequired();
        builder.Property(entity => entity.EnvironmentJson).HasColumnType("text").IsRequired();
        builder.HasIndex(entity => new { entity.RunId, entity.Stage, entity.Name }).IsUnique();
        builder.HasIndex(entity => new { entity.Status, entity.RunsOn });
    }
}
