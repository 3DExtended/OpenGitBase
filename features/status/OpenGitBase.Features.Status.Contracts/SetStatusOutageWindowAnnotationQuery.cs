using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Status.Contracts;

public sealed class SetStatusOutageWindowAnnotationQuery
    : IQuery<AdminStatusOutageWindowDto?, SetStatusOutageWindowAnnotationQuery>
{
    public Guid WindowId { get; set; }

    public string? Annotation { get; set; }
}
