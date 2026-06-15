﻿using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Organization.Contracts;

public class OrganizationDto : ModelBase<OrganizationId, Guid>
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public Guid OwnerUserId { get; set; }
}
