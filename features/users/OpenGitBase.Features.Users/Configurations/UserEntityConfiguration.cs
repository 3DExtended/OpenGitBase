using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.Configurations;

public class UserEntityConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Username).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.NormalizedUsername).HasMaxLength(64).IsRequired();
        builder.HasIndex(entity => entity.NormalizedUsername).IsUnique();
        builder.Property(entity => entity.CreatedAt).IsRequired();
    }
}
