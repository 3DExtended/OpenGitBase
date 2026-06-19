using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.GitAccessToken.Entities;

namespace OpenGitBase.Features.GitAccessToken.Configurations;

public class GitAccessTokenEntityConfiguration : IEntityTypeConfiguration<Entities.GitAccessTokenEntity>
{
    public void Configure(EntityTypeBuilder<Entities.GitAccessTokenEntity> builder)
    {
        builder.ToTable("GitAccessToken");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.TokenLookupHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(entity => entity.TokenLookupHash).IsUnique();
        builder.Property(entity => entity.TokenHash).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.Scope).HasMaxLength(16).IsRequired();
        builder.HasOne(entity => entity.OwnerUser).WithMany().HasForeignKey(entity => entity.OwnerUserId);
    }
}
