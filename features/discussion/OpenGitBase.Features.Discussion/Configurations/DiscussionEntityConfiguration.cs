#pragma warning disable SA1402 // File may only contain a single type
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Discussion.Entities;

namespace OpenGitBase.Features.Discussion.Configurations;

public class DiscussionEntityConfiguration : IEntityTypeConfiguration<DiscussionEntity>
{
    public void Configure(EntityTypeBuilder<DiscussionEntity> builder)
    {
        builder.ToTable("discussions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Title).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.Body).HasMaxLength(8000);
        builder.HasIndex(entity => new { entity.RepositoryId, entity.Number }).IsUnique();
        builder.HasIndex(entity => new { entity.RepositoryId, entity.UpdatedAt });
    }
}

public class DiscussionCommentEntityConfiguration : IEntityTypeConfiguration<DiscussionCommentEntity>
{
    public void Configure(EntityTypeBuilder<DiscussionCommentEntity> builder)
    {
        builder.ToTable("discussion_comments");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.BodyMarkdown).HasMaxLength(16000).IsRequired();
        builder.HasOne(entity => entity.Discussion)
            .WithMany(d => d.Comments)
            .HasForeignKey(entity => entity.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.Anchor)
            .WithOne(a => a.Comment)
            .HasForeignKey<CommentAnchorEntity>(a => a.CommentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CommentAnchorEntityConfiguration : IEntityTypeConfiguration<CommentAnchorEntity>
{
    public void Configure(EntityTypeBuilder<CommentAnchorEntity> builder)
    {
        builder.ToTable("comment_anchors");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Ref).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.CommitSha).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.FilePath).HasMaxLength(2048).IsRequired();
    }
}

public class RepositoryTagEntityConfiguration : IEntityTypeConfiguration<RepositoryTagEntity>
{
    public void Configure(EntityTypeBuilder<RepositoryTagEntity> builder)
    {
        builder.ToTable("repository_tags");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(64).IsRequired();
        builder.HasIndex(entity => new { entity.RepositoryId, entity.Name }).IsUnique();
    }
}

public class DiscussionTagAssignmentEntityConfiguration : IEntityTypeConfiguration<DiscussionTagAssignmentEntity>
{
    public void Configure(EntityTypeBuilder<DiscussionTagAssignmentEntity> builder)
    {
        builder.ToTable("discussion_tag_assignments");
        builder.HasKey(entity => new { entity.DiscussionId, entity.TagId });
        builder.HasOne(entity => entity.Discussion)
            .WithMany(d => d.TagAssignments)
            .HasForeignKey(entity => entity.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.Tag)
            .WithMany(t => t.Assignments)
            .HasForeignKey(entity => entity.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RepositoryBlockedUserEntityConfiguration : IEntityTypeConfiguration<RepositoryBlockedUserEntity>
{
    public void Configure(EntityTypeBuilder<RepositoryBlockedUserEntity> builder)
    {
        builder.ToTable("repository_blocked_users");
        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => new { entity.RepositoryId, entity.UserId }).IsUnique();
    }
}

public class DiscussionSubscriptionEntityConfiguration : IEntityTypeConfiguration<DiscussionSubscriptionEntity>
{
    public void Configure(EntityTypeBuilder<DiscussionSubscriptionEntity> builder)
    {
        builder.ToTable("discussion_subscriptions");
        builder.HasKey(entity => new { entity.DiscussionId, entity.UserId });
        builder.HasOne(entity => entity.Discussion)
            .WithMany(d => d.Subscriptions)
            .HasForeignKey(entity => entity.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserNotificationEntityConfiguration : IEntityTypeConfiguration<UserNotificationEntity>
{
    public void Configure(EntityTypeBuilder<UserNotificationEntity> builder)
    {
        builder.ToTable("user_notifications");
        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => new { entity.UserId, entity.ReadAt, entity.CreatedAt });
        builder.HasOne(entity => entity.Discussion)
            .WithMany()
            .HasForeignKey(entity => entity.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
