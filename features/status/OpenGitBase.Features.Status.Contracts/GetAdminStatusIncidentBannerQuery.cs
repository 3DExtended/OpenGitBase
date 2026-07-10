using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Status.Contracts;

public sealed class GetAdminStatusIncidentBannerQuery
    : IQuery<PublicStatusIncidentDto?, GetAdminStatusIncidentBannerQuery>;
