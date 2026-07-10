using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Organization.Contracts;

public sealed class GetOrganizationStorageSettingsQuery
    : IQuery<OrganizationStorageSettingsDto, GetOrganizationStorageSettingsQuery>
{
    public OrganizationId OrganizationId { get; set; } = default!;
}
