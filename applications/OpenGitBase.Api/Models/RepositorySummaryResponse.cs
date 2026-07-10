using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Models;

public sealed class RepositorySummaryResponse
{
    public RepositoryId Id { get; init; } = default!;

    public string Name { get; init; } = string.Empty;

    public UserId OwnerUserId { get; init; } = default!;

    public string Slug { get; init; } = string.Empty;

    public bool IsPrivate { get; init; }

    public long StorageBytesUsed { get; init; }

    public string OwnerKind { get; init; } = "user";

    public string OwnerSlug { get; init; } = string.Empty;

    public string? DefaultBranchName { get; init; }

    public static RepositorySummaryResponse From(RepositoryDto repository) =>
        new()
        {
            Id = repository.Id,
            Name = repository.Name,
            OwnerUserId = repository.OwnerUserId,
            Slug = repository.Slug,
            IsPrivate = repository.IsPrivate,
            StorageBytesUsed = repository.StorageBytesUsed,
            OwnerKind = repository.OwnerKind,
            OwnerSlug = repository.OwnerSlug,
            DefaultBranchName = repository.DefaultBranchName,
        };
}
