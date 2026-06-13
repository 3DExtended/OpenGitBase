using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenGitBase.Features.RepositoryMember.Entities;

namespace OpenGitBase.Features.RepositoryMember.Configurations;

public class RepositoryMemberEntityConfiguration
    : IEntityTypeConfiguration<Entities.RepositoryMemberEntity>
{
    public void Configure(EntityTypeBuilder<Entities.RepositoryMemberEntity> builder)
    {
        builder.ToTable("RepositoryMember");
        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => new { entity.RepositoryId, entity.UserId }).IsUnique();
    }
}
