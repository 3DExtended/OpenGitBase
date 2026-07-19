using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Status.Contracts;

public sealed class SuppressStatusOutageWindowQuery
    : IQuery<AdminStatusOutageWindowDto?, SuppressStatusOutageWindowQuery>
{
    public Guid WindowId { get; set; }

    public bool Suppressed { get; set; }
}
