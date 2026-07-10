using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Status.Contracts;

public sealed class GetPublicStatusHistoryQuery : IQuery<PublicStatusHistoryDto, GetPublicStatusHistoryQuery>
{
    public int Days { get; set; } = 90;
}
