using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenGitBase.Features.StorageNode.Configurations;

public class StorageNodeEntityConfiguration : IEntityTypeConfiguration<Entities.StorageNodeEntity>
{
    public void Configure(EntityTypeBuilder<Entities.StorageNodeEntity> builder)
    {
        builder.ToTable("StorageNode");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.NodeId).HasMaxLength(256).IsRequired();
        builder.HasIndex(entity => entity.NodeId).IsUnique();
        builder.Property(entity => entity.InternalHost).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.ApiTokenHash).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.ApiTokenProtected).HasMaxLength(2048).IsRequired();
    }
}
