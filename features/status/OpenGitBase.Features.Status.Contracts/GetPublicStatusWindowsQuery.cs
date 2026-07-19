using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Status.Contracts;

public sealed class GetPublicStatusWindowsQuery
    : IQuery<List<PublicStatusOutageWindowDto>, GetPublicStatusWindowsQuery>
{
    public int Days { get; set; } = 7;
}
