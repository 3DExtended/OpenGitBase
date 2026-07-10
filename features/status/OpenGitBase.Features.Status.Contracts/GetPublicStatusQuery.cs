using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Status.Contracts;

public sealed class GetPublicStatusQuery : IQuery<PublicStatusSnapshotDto, GetPublicStatusQuery>;
