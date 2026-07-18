using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.Configurations;

public sealed class GitPushOutboxEntityConfiguration : IEntityTypeConfiguration<GitPushOutboxEntity>
{
    public void Configure(EntityTypeBuilder<GitPushOutboxEntity> builder)
    {
        builder.ToTable("GitPushOutbox");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Ref).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.AfterSha).HasMaxLength(64).IsRequired();
        builder.HasIndex(entity => new { entity.RepositoryId, entity.AfterSha }).IsUnique();
        builder.HasIndex(entity => new { entity.Status, entity.CreatedAt });
    }
}
