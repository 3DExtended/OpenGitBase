using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.AccountSecurity.Entities;

namespace OpenGitBase.Features.AccountSecurity.Configurations;

public class UserRecoveryCodeEntityConfiguration : IEntityTypeConfiguration<UserRecoveryCodeEntity>
{
    public void Configure(EntityTypeBuilder<UserRecoveryCodeEntity> builder)
    {
        builder.ToTable("UserRecoveryCodes");
        builder.HasKey(entity => entity.Id);
        builder
            .HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(entity => entity.CodeHash).HasMaxLength(256).IsRequired();
        builder.HasIndex(entity => entity.UserId);
    }
}
