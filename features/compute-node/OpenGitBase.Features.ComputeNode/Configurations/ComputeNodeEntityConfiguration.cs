using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.ComputeNode.Entities;

namespace OpenGitBase.Features.ComputeNode.Configurations;

public sealed class ComputeNodeEntityConfiguration : IEntityTypeConfiguration<ComputeNodeEntity>
{
    public void Configure(EntityTypeBuilder<ComputeNodeEntity> builder)
    {
        builder.ToTable("ComputeNode");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.NodeId).HasMaxLength(256).IsRequired();
        builder.HasIndex(entity => entity.NodeId).IsUnique();
    }
}
