﻿using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class VerifyStorageNodeTokenQuery
    : IQuery<StorageNodeId, VerifyStorageNodeTokenQuery>
{
    public string NodeId { get; set; } = string.Empty;

    public string ApiToken { get; set; } = string.Empty;
}
