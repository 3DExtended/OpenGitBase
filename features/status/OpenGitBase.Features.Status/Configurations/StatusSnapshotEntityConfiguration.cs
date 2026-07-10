using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Status.Entities;

namespace OpenGitBase.Features.Status.Configurations;

public class StatusSnapshotEntityConfiguration : IEntityTypeConfiguration<StatusSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<StatusSnapshotEntity> builder)
    {
        builder.ToTable("StatusSnapshot");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.PayloadJson).IsRequired();
    }
}
