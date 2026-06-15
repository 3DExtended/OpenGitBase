﻿using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Features.Organization.Contracts;

public class DeleteOrganizationQuery
    : DeleteCommand<OrganizationDto, OrganizationId, Guid, DeleteOrganizationQuery>;
