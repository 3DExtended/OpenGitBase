using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.Configurations;

public sealed class JobIdentityEntityConfiguration : IEntityTypeConfiguration<JobIdentityEntity>
{
    public void Configure(EntityTypeBuilder<JobIdentityEntity> builder)
    {
        builder.ToTable("JobIdentity");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TokenHash).HasMaxLength(512).IsRequired();
        builder.HasIndex(entity => entity.JobId).IsUnique();
    }
}
