using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.AccountSecurity.Entities;

namespace OpenGitBase.Features.AccountSecurity.Configurations;

public class AuthenticationChallengeEntityConfiguration : IEntityTypeConfiguration<AuthenticationChallengeEntity>
{
    public void Configure(EntityTypeBuilder<AuthenticationChallengeEntity> builder)
    {
        builder.ToTable("AuthenticationChallenges");
        builder.HasKey(entity => entity.Id);
        builder
            .HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(entity => entity.Purpose).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(entity => new { entity.Purpose, entity.TokenHash }).IsUnique();
        builder.HasIndex(entity => entity.UserId);
    }
}
