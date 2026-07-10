using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.Configurations;

public class RepositoryKeyEntityConfiguration : IEntityTypeConfiguration<RepositoryKeyEntity>
{
    public void Configure(EntityTypeBuilder<RepositoryKeyEntity> builder)
    {
        builder.ToTable("RepositoryKey");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RepositoryId).IsRequired();
        builder.Property(entity => entity.KeyCiphertext).IsRequired();
        builder.Property(entity => entity.KeyVersion).IsRequired();
        builder.Property(entity => entity.CreatedAt).IsRequired();
        builder.HasIndex(entity => entity.RepositoryId).IsUnique();
        builder
            .HasOne(entity => entity.Repository)
            .WithOne()
            .HasForeignKey<RepositoryKeyEntity>(entity => entity.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
