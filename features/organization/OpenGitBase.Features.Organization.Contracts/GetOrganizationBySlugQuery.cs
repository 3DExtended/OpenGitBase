﻿using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Features.Organization.Contracts;

public class GetOrganizationBySlugQuery : IQuery<OrganizationDto, GetOrganizationBySlugQuery>
{
    public string Slug { get; set; } = string.Empty;
}
