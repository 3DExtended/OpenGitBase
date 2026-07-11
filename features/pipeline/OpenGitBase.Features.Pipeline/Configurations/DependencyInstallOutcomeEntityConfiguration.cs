using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.Configurations;

public sealed class DependencyInstallOutcomeEntityConfiguration
    : IEntityTypeConfiguration<DependencyInstallOutcomeEntity>
{
    public void Configure(EntityTypeBuilder<DependencyInstallOutcomeEntity> builder)
    {
        builder.ToTable("DependencyInstallOutcome");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RecipeKey).HasMaxLength(256).IsRequired();
        builder.HasIndex(entity => new { entity.RecipeKey, entity.CreatedAt });
    }
}
