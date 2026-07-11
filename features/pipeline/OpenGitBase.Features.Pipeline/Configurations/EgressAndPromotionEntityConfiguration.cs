using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.Configurations;

#pragma warning disable SA1402

public sealed class DependencyPromotionRequestEntityConfiguration
    : IEntityTypeConfiguration<DependencyPromotionRequestEntity>
{
    public void Configure(EntityTypeBuilder<DependencyPromotionRequestEntity> builder)
    {
        builder.ToTable("DependencyPromotionRequest");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RecipeKey).HasMaxLength(256).IsRequired();
        builder.HasIndex(entity => new { entity.RecipeKey, entity.CreatedAt });
    }
}

public sealed class DomainAllowanceRequestEntityConfiguration
    : IEntityTypeConfiguration<DomainAllowanceRequestEntity>
{
    public void Configure(EntityTypeBuilder<DomainAllowanceRequestEntity> builder)
    {
        builder.ToTable("DomainAllowanceRequest");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Domain).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.Justification).HasMaxLength(4000).IsRequired();
        builder.HasIndex(entity => new { entity.Scope, entity.OrganizationId, entity.Status });
    }
}

public sealed class PlatformEgressAllowlistEntityConfiguration
    : IEntityTypeConfiguration<PlatformEgressAllowlistEntity>
{
    public void Configure(EntityTypeBuilder<PlatformEgressAllowlistEntity> builder)
    {
        builder.ToTable("PlatformEgressAllowlist");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Domain).HasMaxLength(256).IsRequired();
        builder.HasIndex(entity => entity.Domain).IsUnique();
    }
}

public sealed class OrgEgressAllowlistEntityConfiguration
    : IEntityTypeConfiguration<OrgEgressAllowlistEntity>
{
    public void Configure(EntityTypeBuilder<OrgEgressAllowlistEntity> builder)
    {
        builder.ToTable("OrgEgressAllowlist");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Domain).HasMaxLength(256).IsRequired();
        builder.HasIndex(entity => new { entity.OrganizationId, entity.Domain }).IsUnique();
    }
}

#pragma warning restore SA1402
