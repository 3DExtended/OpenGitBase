using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.Configurations;

public class OrganizationEntityConfiguration : IEntityTypeConfiguration<OrganizationEntity>
{
    public void Configure(EntityTypeBuilder<OrganizationEntity> builder)
    {
        builder.ToTable("Organization");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.Slug).HasMaxLength(256).IsRequired();
        builder.HasIndex(entity => entity.Slug).IsUnique();
        builder.Property(entity => entity.OwnerUserId).IsRequired();
    }
}
