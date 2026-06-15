﻿using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Features.Organization.Contracts;

public class CreateOrganizationQuery
    : CreateQuery<OrganizationDto, OrganizationId, Guid, CreateOrganizationQuery>
{
    public Guid CreatorUserId { get; set; }
}
