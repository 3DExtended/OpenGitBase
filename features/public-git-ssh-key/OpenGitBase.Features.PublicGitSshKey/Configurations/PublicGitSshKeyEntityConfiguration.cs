using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.PublicGitSshKey.Entities;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.PublicGitSshKey.Configurations;

public class PublicGitSshKeyEntityConfiguration
    : IEntityTypeConfiguration<Entities.PublicGitSshKeyEntity>
{
    public void Configure(EntityTypeBuilder<Entities.PublicGitSshKeyEntity> builder)
    {
        builder.ToTable("PublicGitSshKey");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.PublicSSHKey).HasMaxLength(4096).IsRequired();

        builder.HasIndex(e => e.Fingerprint).IsUnique();
        builder.HasIndex(e => e.PublicSSHKey).IsUnique();

        builder.HasIndex(e => new { e.Fingerprint, e.PublicSSHKey });
        builder.HasIndex(e => new { e.OwnerUserId, e.Fingerprint });
    }
}
