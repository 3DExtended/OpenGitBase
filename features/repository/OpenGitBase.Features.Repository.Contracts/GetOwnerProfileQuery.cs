﻿using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public class GetOwnerProfileQuery : IQuery<OwnerProfileDto, GetOwnerProfileQuery>
{
    public string OwnerSlug { get; set; } = string.Empty;
}
