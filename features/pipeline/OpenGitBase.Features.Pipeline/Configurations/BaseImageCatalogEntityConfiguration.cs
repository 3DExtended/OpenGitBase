using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.Configurations;

public sealed class BaseImageCatalogEntityConfiguration
    : IEntityTypeConfiguration<BaseImageCatalogEntity>
{
    public void Configure(EntityTypeBuilder<BaseImageCatalogEntity> builder)
    {
        builder.ToTable("BaseImageCatalog");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Slug).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.VersionLabel).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.ArtifactUri).HasMaxLength(1024).IsRequired();
        builder.Property(entity => entity.OciProvenance).HasColumnType("text").IsRequired();
        builder.HasIndex(entity => entity.Slug).IsUnique();
    }
}
