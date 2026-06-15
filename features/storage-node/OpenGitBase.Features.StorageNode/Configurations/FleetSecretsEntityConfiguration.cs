using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenGitBase.Features.StorageNode.Configurations;

public class FleetSecretsEntityConfiguration : IEntityTypeConfiguration<Entities.FleetSecretsEntity>
{
    public void Configure(EntityTypeBuilder<Entities.FleetSecretsEntity> builder)
    {
        builder.ToTable("FleetSecrets");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.DispatcherSshPublicKey).HasMaxLength(4096).IsRequired();
        builder.Property(entity => entity.DispatcherSshPrivateKeyProtected).HasMaxLength(8192).IsRequired();
        builder.Property(entity => entity.FleetBootstrapTokenHash).HasMaxLength(512).IsRequired();
    }
}
