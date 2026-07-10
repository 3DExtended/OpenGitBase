using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Status.Entities;

namespace OpenGitBase.Features.Status.Configurations;

public class StatusIncidentBannerEntityConfiguration
    : IEntityTypeConfiguration<StatusIncidentBannerEntity>
{
    public void Configure(EntityTypeBuilder<StatusIncidentBannerEntity> builder)
    {
        builder.ToTable("StatusIncidentBanner");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Message).HasMaxLength(500).IsRequired();
    }
}
