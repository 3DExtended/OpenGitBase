﻿using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Features.Organization.Contracts;

public class ListOrganizationMembersQuery
    : IQuery<IReadOnlyList<OrganizationMemberDto>, ListOrganizationMembersQuery>
{
    public OrganizationId OrganizationId { get; set; } = default!;
}
