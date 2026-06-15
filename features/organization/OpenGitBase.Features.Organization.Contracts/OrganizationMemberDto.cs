﻿using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Organization.Contracts;

public class OrganizationMemberDto : ModelBase<OrganizationMemberId, Guid>
{
    public OrganizationId OrganizationId { get; set; } = default!;

    public UserId UserId { get; set; } = default!;

    public string? Username { get; set; }

    public OrganizationMemberRole Role { get; set; }
}
