using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.MergeRequest.Entities;

namespace OpenGitBase.Features.MergeRequest.Configurations;

public class MergeRequestEntityConfiguration : IEntityTypeConfiguration<Entities.MergeRequestEntity>
{
    public void Configure(EntityTypeBuilder<Entities.MergeRequestEntity> builder)
    {
        builder.ToTable("merge_requests");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Title).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.Body).HasMaxLength(16000);
        builder.Property(entity => entity.SourceRef).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.TargetRef).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.SourceHeadSha).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.TargetBaseSha).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.MergeCommitSha).HasMaxLength(64);
        builder.HasIndex(entity => new { entity.RepositoryId, entity.Number }).IsUnique();
        builder.HasIndex(entity => new { entity.RepositoryId, entity.UpdatedAt });
        builder.HasIndex(entity => new { entity.RepositoryId, entity.SourceRef, entity.TargetRef });
    }
}
