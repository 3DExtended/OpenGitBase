using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Organization.Contracts;

public sealed class GetOrganizationStorageQuotaQuery
    : IQuery<OrganizationStorageQuotaDto, GetOrganizationStorageQuotaQuery>
{
    public OrganizationId OrganizationId { get; set; } = default!;
}
