namespace OpenGitBase.Api.Services;

/// <summary>A storage node's internal HTTP API and the token it accepts.</summary>
public sealed record StorageNodeEndpoint(Guid StorageNodeId, string InternalHost, int InternalHttpPort, string ApiToken);
