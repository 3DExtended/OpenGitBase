using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.Configurations;

public class OrganizationStorageSettingsEntityConfiguration
    : IEntityTypeConfiguration<OrganizationStorageSettingsEntity>
{
    public void Configure(EntityTypeBuilder<OrganizationStorageSettingsEntity> builder)
    {
        builder.ToTable("OrganizationStorageSettings");
        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => entity.OrganizationId).IsUnique();
    }
}
