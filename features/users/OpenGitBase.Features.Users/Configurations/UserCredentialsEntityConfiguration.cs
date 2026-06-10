using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.Configurations;

public class UserCredentialsEntityConfiguration : IEntityTypeConfiguration<UserCredentialsEntity>
{
    public void Configure(EntityTypeBuilder<UserCredentialsEntity> builder)
    {
        builder.ToTable("UserCredentials");
        builder.HasKey(entity => entity.UserId);

        builder
            .HasOne(entity => entity.User)
            .WithOne(user => user.UserCredentials)
            .HasForeignKey<UserCredentialsEntity>(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.Property(entity => entity.Username).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.PasswordHash).HasMaxLength(512);
        builder.Property(entity => entity.InternalId).HasMaxLength(256);
        builder.Property(entity => entity.EmailCiphertext).HasMaxLength(1024);
        builder.Property(entity => entity.EmailLookupHash).HasMaxLength(128);
        builder.HasIndex(entity => entity.EmailLookupHash).IsUnique();
        builder.Property(entity => entity.PasswordResetTokenHash).HasMaxLength(512);
    }
}
