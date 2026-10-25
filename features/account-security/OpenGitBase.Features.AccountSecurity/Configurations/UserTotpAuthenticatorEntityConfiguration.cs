using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.AccountSecurity.Entities;

namespace OpenGitBase.Features.AccountSecurity.Configurations;

public class UserTotpAuthenticatorEntityConfiguration : IEntityTypeConfiguration<UserTotpAuthenticatorEntity>
{
    public void Configure(EntityTypeBuilder<UserTotpAuthenticatorEntity> builder)
    {
        builder.ToTable("UserTotpAuthenticators");
        builder.HasKey(entity => entity.UserId);
        builder
            .HasOne(entity => entity.User)
            .WithOne()
            .HasForeignKey<UserTotpAuthenticatorEntity>(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(entity => entity.SecretProtected).HasMaxLength(512);
        builder.Property(entity => entity.PendingSecretProtected).HasMaxLength(512);
    }
}
