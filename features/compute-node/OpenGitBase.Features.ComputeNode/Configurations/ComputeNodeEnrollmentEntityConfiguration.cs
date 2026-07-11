using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.ComputeNode.Entities;

namespace OpenGitBase.Features.ComputeNode.Configurations;

public sealed class ComputeNodeEnrollmentEntityConfiguration
    : IEntityTypeConfiguration<ComputeNodeEnrollmentEntity>
{
    public void Configure(EntityTypeBuilder<ComputeNodeEnrollmentEntity> builder)
    {
        builder.ToTable("ComputeNodeEnrollment");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.NodeId).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.EnrollmentTokenHash).HasMaxLength(512).IsRequired();
        builder.HasIndex(entity => entity.NodeId);
    }
}
