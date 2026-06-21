using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.Configurations;

public class RepositoryEntityConfiguration : IEntityTypeConfiguration<Entities.RepositoryEntity>
{
    public void Configure(EntityTypeBuilder<Entities.RepositoryEntity> builder)
    {
        builder.ToTable("Repository");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.Slug).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.PhysicalPath).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.OwnerUserId).IsRequired();
        builder.Property(entity => entity.StorageNodeId);
        builder.Property(entity => entity.PrimaryStorageNodeId);
        builder.Property(entity => entity.ReplicationEpoch).IsRequired();
        builder.Property(entity => entity.PrimaryWatermark).IsRequired();
        builder.Property(entity => entity.ReplicationState).IsRequired();
        builder.HasIndex(e => new { e.Slug, e.OwnerUserId }).IsUnique();
    }
}
