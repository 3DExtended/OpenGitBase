﻿using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class GetStorageNodeByNodeIdQuery
    : IQuery<StorageNodeDto, GetStorageNodeByNodeIdQuery>
{
    public string NodeId { get; set; } = string.Empty;
}
