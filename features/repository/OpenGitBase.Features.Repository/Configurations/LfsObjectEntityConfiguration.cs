using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.Configurations;

public class LfsObjectEntityConfiguration : IEntityTypeConfiguration<LfsObjectEntity>
{
    public void Configure(EntityTypeBuilder<LfsObjectEntity> builder)
    {
        builder.ToTable("LfsObject");
        // The key makes (RepositoryId, Oid) unique, so concurrent verifies of one oid insert one row.
        builder.HasKey(entity => new { entity.RepositoryId, entity.Oid });
        builder.Property(entity => entity.Oid).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Size).IsRequired();
        builder.Property(entity => entity.KeyVersion).IsRequired();
        builder.Property(entity => entity.CreatedAt).IsRequired();
        builder
            .HasOne(entity => entity.Repository)
            .WithMany()
            .HasForeignKey(entity => entity.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
