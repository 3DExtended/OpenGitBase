using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Status.Entities;

namespace OpenGitBase.Features.Status.Configurations;

public class StatusHistoryHourlyBucketEntityConfiguration
    : IEntityTypeConfiguration<StatusHistoryHourlyBucketEntity>
{
    public void Configure(EntityTypeBuilder<StatusHistoryHourlyBucketEntity> builder)
    {
        builder.ToTable("StatusHistoryHourlyBucket");
        builder.HasKey(entity => entity.Id);
        builder
            .HasIndex(entity => new { entity.ComponentGroup, entity.PeriodStartUtc })
            .IsUnique();
    }
}
