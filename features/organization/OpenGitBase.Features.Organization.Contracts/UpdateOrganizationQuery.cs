﻿using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Features.Organization.Contracts;

public class UpdateOrganizationQuery
    : UpdateCommand<OrganizationDto, OrganizationId, Guid, UpdateOrganizationQuery>;
