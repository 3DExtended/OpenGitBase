namespace OpenGitBase.Features.StorageNode.Contracts;

public readonly record struct StorageNodeEnrollmentId(Guid Value)
{
    public static StorageNodeEnrollmentId From(Guid value) => new(value);
}
