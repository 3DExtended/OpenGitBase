#pragma warning disable SA1402 // File may only contain a single type
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.MergeRequest.Entities;

namespace OpenGitBase.Features.MergeRequest.Configurations;

public class MergeRequestApprovalEntityConfiguration
    : IEntityTypeConfiguration<MergeRequestApprovalEntity>
{
    public void Configure(EntityTypeBuilder<MergeRequestApprovalEntity> builder)
    {
        builder.ToTable("merge_request_approvals");
        builder.HasKey(entity => new { entity.MergeRequestId, entity.UserId });
        builder.Property(entity => entity.CommitSha).HasMaxLength(64).IsRequired();
        builder
            .HasOne(entity => entity.MergeRequest)
            .WithMany()
            .HasForeignKey(entity => entity.MergeRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MergeRequestDiscussionLinkEntityConfiguration
    : IEntityTypeConfiguration<MergeRequestDiscussionLinkEntity>
{
    public void Configure(EntityTypeBuilder<MergeRequestDiscussionLinkEntity> builder)
    {
        builder.ToTable("merge_request_discussion_links");
        builder.HasKey(entity => new
        {
            entity.MergeRequestId,
            entity.DiscussionId,
            entity.RelationshipType,
        });
        builder
            .HasOne(entity => entity.MergeRequest)
            .WithMany()
            .HasForeignKey(entity => entity.MergeRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
