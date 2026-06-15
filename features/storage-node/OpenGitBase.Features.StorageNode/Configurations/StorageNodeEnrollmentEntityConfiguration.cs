using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenGitBase.Features.StorageNode.Configurations;

public class StorageNodeEnrollmentEntityConfiguration
    : IEntityTypeConfiguration<Entities.StorageNodeEnrollmentEntity>
{
    public void Configure(EntityTypeBuilder<Entities.StorageNodeEnrollmentEntity> builder)
    {
        builder.ToTable("StorageNodeEnrollment");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.NodeId).HasMaxLength(256).IsRequired();
        builder.HasIndex(entity => entity.NodeId);
        builder.Property(entity => entity.EnrollmentTokenHash).HasMaxLength(512).IsRequired();
    }
}
