﻿using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class RegisterStorageNodeQuery
    : IQuery<RegisterStorageNodeResult, RegisterStorageNodeQuery>
{
    public string NodeId { get; set; } = string.Empty;

    public string InternalHost { get; set; } = string.Empty;

    public int InternalSshPort { get; set; } = 22;

    public int InternalHttpPort { get; set; }

    public long FreeBytesAvailable { get; set; }

    public long TotalBytesAvailable { get; set; }

    public string EnrollmentToken { get; set; } = string.Empty;
}
