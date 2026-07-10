using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Status.Entities;

namespace OpenGitBase.Features.Status.Configurations;

public class FleetComponentEntityConfiguration : IEntityTypeConfiguration<FleetComponentEntity>
{
    public void Configure(EntityTypeBuilder<FleetComponentEntity> builder)
    {
        builder.ToTable("FleetComponent");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.InstanceId).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.ProbeUrl).HasMaxLength(2048).IsRequired();
        builder.Property(entity => entity.Version).HasMaxLength(128);
        builder
            .HasIndex(entity => new { entity.ComponentType, entity.InstanceId })
            .IsUnique();
    }
}
