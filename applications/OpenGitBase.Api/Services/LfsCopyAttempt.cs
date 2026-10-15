using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

/// <summary>The outcome of copying an LFS object to one storage node.</summary>
public sealed record LfsCopyAttempt(StorageNodeEndpoint Node, LfsObjectCopyKind Kind, bool Succeeded);
