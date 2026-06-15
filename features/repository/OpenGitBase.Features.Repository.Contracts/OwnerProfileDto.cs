﻿using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public class OwnerProfileDto
{
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Kind { get; set; } = "user";

    public IReadOnlyList<RepositoryDto> Repositories { get; set; } = [];
}
