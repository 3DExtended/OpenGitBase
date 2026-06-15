using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.Configurations;

public class OrganizationMemberEntityConfiguration
    : IEntityTypeConfiguration<OrganizationMemberEntity>
{
    public void Configure(EntityTypeBuilder<OrganizationMemberEntity> builder)
    {
        builder.ToTable("OrganizationMember");
        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.UserId }).IsUnique();
        builder.Property(entity => entity.Role).IsRequired();
    }
}
