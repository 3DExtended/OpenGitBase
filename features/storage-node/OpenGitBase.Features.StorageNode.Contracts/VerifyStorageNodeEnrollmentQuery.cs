using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class VerifyStorageNodeEnrollmentQuery
    : IQuery<StorageNodeEnrollmentId, VerifyStorageNodeEnrollmentQuery>
{
    public string NodeId { get; set; } = string.Empty;

    public string EnrollmentToken { get; set; } = string.Empty;

    public bool Consume { get; set; } = true;
}
