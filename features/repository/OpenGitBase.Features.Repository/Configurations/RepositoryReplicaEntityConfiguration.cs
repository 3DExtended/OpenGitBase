using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.Configurations;

public class RepositoryReplicaEntityConfiguration
    : IEntityTypeConfiguration<RepositoryReplicaEntity>
{
    public void Configure(EntityTypeBuilder<RepositoryReplicaEntity> builder)
    {
        builder.ToTable("RepositoryReplica");
        builder.HasKey(entity => new { entity.RepositoryId, entity.StorageNodeId });
        builder.Property(entity => entity.Role).IsRequired();
        builder.Property(entity => entity.AppliedWatermark).IsRequired();
        builder
            .HasOne(entity => entity.Repository)
            .WithMany(repository => repository.Replicas)
            .HasForeignKey(entity => entity.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
