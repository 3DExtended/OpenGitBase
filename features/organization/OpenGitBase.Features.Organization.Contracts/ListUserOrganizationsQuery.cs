﻿using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Organization.Contracts;

public class ListUserOrganizationsQuery
    : IQuery<IReadOnlyList<OrganizationDto>, ListUserOrganizationsQuery>
{
    public UserId UserId { get; set; } = default!;
}
