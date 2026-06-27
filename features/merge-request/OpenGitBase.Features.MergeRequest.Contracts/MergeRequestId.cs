﻿using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.MergeRequest.Contracts;

public record MergeRequestId : Identifier<Guid, MergeRequestId>;
