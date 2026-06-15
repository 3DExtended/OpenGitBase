using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class ListStorageNodeEnrollmentsQuery
    : IQuery<IReadOnlyList<StorageNodeEnrollmentDto>, ListStorageNodeEnrollmentsQuery>;
