﻿using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Features.Organization.Contracts;

public class GetOrganizationQuery
    : SingleModelQuery<OrganizationDto, OrganizationId, Guid, GetOrganizationQuery>;
