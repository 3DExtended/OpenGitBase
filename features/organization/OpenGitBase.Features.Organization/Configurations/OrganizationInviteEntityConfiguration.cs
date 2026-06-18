using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.Configurations;

public class OrganizationInviteEntityConfiguration : IEntityTypeConfiguration<OrganizationInviteEntity>
{
    public void Configure(EntityTypeBuilder<OrganizationInviteEntity> builder)
    {
        builder.ToTable("OrganizationInvite");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.EmailLookupHash).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.EmailCiphertext).HasMaxLength(1024).IsRequired();
        builder.Property(entity => entity.TokenHash).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.Role).IsRequired();
        builder.Property(entity => entity.Status).IsRequired();
        builder.Property(entity => entity.CreatedAt).IsRequired();
        builder.Property(entity => entity.ExpiresAt).IsRequired();
        builder.HasIndex(entity => new { entity.OrganizationId, entity.EmailLookupHash });
        builder.HasIndex(entity => entity.ExpiresAt);
    }
}
