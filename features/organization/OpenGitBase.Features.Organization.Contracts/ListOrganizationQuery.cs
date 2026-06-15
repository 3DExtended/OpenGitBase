﻿using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Features.Organization.Contracts;

public class ListOrganizationQuery
    : ListOfModelQuery<OrganizationDto, OrganizationId, Guid, ListOrganizationQuery>;
