using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.Configurations;

public sealed class JobStatusTransitionEntityConfiguration
    : IEntityTypeConfiguration<JobStatusTransitionEntity>
{
    public void Configure(EntityTypeBuilder<JobStatusTransitionEntity> builder)
    {
        builder.ToTable("JobStatusTransition");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Message).HasMaxLength(1024).IsRequired();
        builder.HasIndex(entity => new { entity.JobId, entity.CreatedAt });
    }
}
