using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.AccountSecurity.Entities;

namespace OpenGitBase.Features.AccountSecurity.Configurations;

public class UserPasskeyEntityConfiguration : IEntityTypeConfiguration<UserPasskeyEntity>
{
    public void Configure(EntityTypeBuilder<UserPasskeyEntity> builder)
    {
        builder.ToTable("UserPasskeys");
        builder.HasKey(entity => entity.Id);
        builder
            .HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(entity => entity.CredentialId).HasMaxLength(1023).IsRequired();
        builder.Property(entity => entity.PublicKey).IsRequired();
        builder.Property(entity => entity.Transports).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(64).IsRequired();
        builder.HasIndex(entity => entity.CredentialId).IsUnique();
        builder.HasIndex(entity => entity.UserId);
    }
}
