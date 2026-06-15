﻿using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Organization.Contracts;

public class RemoveOrganizationMemberQuery : IQuery<Unit, RemoveOrganizationMemberQuery>
{
    public OrganizationId OrganizationId { get; set; } = default!;

    public UserId UserId { get; set; } = default!;
}
