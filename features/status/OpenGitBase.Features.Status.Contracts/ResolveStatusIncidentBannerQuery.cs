using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Status.Contracts;

public sealed class ResolveStatusIncidentBannerQuery
    : IQuery<bool, ResolveStatusIncidentBannerQuery>;
