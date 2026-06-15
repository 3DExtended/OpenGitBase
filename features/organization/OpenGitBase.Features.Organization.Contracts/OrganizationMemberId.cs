﻿using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Organization.Contracts;

public record OrganizationMemberId : Identifier<Guid, OrganizationMemberId>;
