#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1402 // File may only contain a single type
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.Configurations;

public class ProtectedBranchRuleEntityConfiguration
    : IEntityTypeConfiguration<ProtectedBranchRuleEntity>
{
    public void Configure(EntityTypeBuilder<ProtectedBranchRuleEntity> builder)
    {
        builder.ToTable("ProtectedBranchRule");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RepositoryId).IsRequired();
        builder.Property(entity => entity.Pattern).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.BlockDirectPush).IsRequired();
        builder.Property(entity => entity.AllowedPushRoles).IsRequired();
        builder.Property(entity => entity.RequireMergeRequest).IsRequired();
        builder.Property(entity => entity.RequiredApprovalCount).IsRequired();
        builder.Property(entity => entity.MergeRoleThreshold).IsRequired();
        builder.Property(entity => entity.ForcePushPolicy).IsRequired();
        builder.Property(entity => entity.DismissApprovalsOnPush).IsRequired();
        builder.Property(entity => entity.LockedMergeStrategy);

        builder.HasIndex(entity => new { entity.RepositoryId, entity.Pattern }).IsUnique();

        builder
            .HasMany(entity => entity.AllowedUsers)
            .WithOne(entity => entity.ProtectedBranchRule)
            .HasForeignKey(entity => entity.ProtectedBranchRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(entity => entity.PushRules)
            .WithOne(entity => entity.ProtectedBranchRule)
            .HasForeignKey(entity => entity.ProtectedBranchRuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProtectedBranchAllowedUserEntityConfiguration
    : IEntityTypeConfiguration<ProtectedBranchAllowedUserEntity>
{
    public void Configure(EntityTypeBuilder<ProtectedBranchAllowedUserEntity> builder)
    {
        builder.ToTable("ProtectedBranchAllowedUser");
        builder.HasKey(entity => new { entity.ProtectedBranchRuleId, entity.UserId });
        builder.Property(entity => entity.UserId).IsRequired();
    }
}

public class PushRuleEntityConfiguration : IEntityTypeConfiguration<PushRuleEntity>
{
    public void Configure(EntityTypeBuilder<PushRuleEntity> builder)
    {
        builder.ToTable("PushRule");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ProtectedBranchRuleId).IsRequired();
        builder.Property(entity => entity.RuleType).IsRequired();
        builder.Property(entity => entity.ConfigJson).HasColumnType("text").IsRequired();
    }
}
