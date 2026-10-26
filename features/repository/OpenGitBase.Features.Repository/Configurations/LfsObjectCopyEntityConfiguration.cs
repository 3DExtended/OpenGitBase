using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.Configurations;

public class LfsObjectCopyEntityConfiguration : IEntityTypeConfiguration<LfsObjectCopyEntity>
{
    public void Configure(EntityTypeBuilder<LfsObjectCopyEntity> builder)
    {
        builder.ToTable("LfsObjectCopy");

        // A node holds either the plaintext or the encrypted copy of a repository's objects, never both.
        builder.HasKey(entity => new { entity.RepositoryId, entity.Oid, entity.StorageNodeId });
        builder.Property(entity => entity.Oid).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Kind).IsRequired();
        builder.Property(entity => entity.StoredAt).IsRequired();
        builder.HasIndex(entity => entity.StorageNodeId);

        builder
            .HasOne(entity => entity.Object)
            .WithMany()
            .HasForeignKey(entity => new { entity.RepositoryId, entity.Oid })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
