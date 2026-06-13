using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.RepositoryMember.Tests.Testing;

public sealed record RepositoryMemberSeedData(
    RepositoryId RepositoryId,
    UserId OwnerUserId,
    UserId MemberUserId,
    RepositoryMemberId MemberId,
    Entities.RepositoryMemberEntity MemberEntity
);
