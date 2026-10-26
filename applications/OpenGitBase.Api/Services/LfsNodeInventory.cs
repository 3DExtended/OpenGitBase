using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

/// <summary>What a storage node holds on disk for one repository: its copy kind and each oid with its stored size.</summary>
public sealed record LfsNodeInventory(LfsObjectCopyKind Kind, IReadOnlyDictionary<string, long> Objects);
