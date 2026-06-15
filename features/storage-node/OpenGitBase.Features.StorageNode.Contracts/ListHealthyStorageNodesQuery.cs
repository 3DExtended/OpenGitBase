﻿using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class ListHealthyStorageNodesQuery
    : IQuery<IReadOnlyList<StorageNodeDto>, ListHealthyStorageNodesQuery>;
