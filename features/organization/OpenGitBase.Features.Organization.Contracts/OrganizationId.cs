﻿using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Organization.Contracts;

public record OrganizationId : Identifier<Guid, OrganizationId>;
