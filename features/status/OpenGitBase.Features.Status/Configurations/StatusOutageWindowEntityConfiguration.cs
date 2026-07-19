using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Status.Entities;

namespace OpenGitBase.Features.Status.Configurations;

public class StatusOutageWindowEntityConfiguration
    : IEntityTypeConfiguration<StatusOutageWindowEntity>
{
    public void Configure(EntityTypeBuilder<StatusOutageWindowEntity> builder)
    {
        builder.ToTable("StatusOutageWindow");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.DisplayName).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.InstanceId).HasMaxLength(256);
        builder.Property(entity => entity.Annotation).HasMaxLength(2000);

        builder
            .HasIndex(entity => new
            {
                entity.Scope,
                entity.ComponentGroup,
                entity.InstanceId,
                entity.EndedAt,
            })
            .HasDatabaseName("IX_StatusOutageWindow_ActiveKey");

        builder
            .HasIndex(entity => entity.UnhealthySince)
            .HasDatabaseName("IX_StatusOutageWindow_UnhealthySince");

        builder
            .HasIndex(entity => new { entity.BecamePublicAt, entity.Suppressed, entity.EndedAt })
            .HasDatabaseName("IX_StatusOutageWindow_PublicList");
    }
}
