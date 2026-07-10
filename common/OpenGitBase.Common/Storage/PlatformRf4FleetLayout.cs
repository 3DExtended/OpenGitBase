namespace OpenGitBase.Common.Storage;

/// <summary>
/// Local three-node compose fleet role mapping for RF=4 development.
/// Primary and read replica colocate on storage-1; encrypted replicas use storage-2 and storage-3.
/// </summary>
public static class PlatformRf4FleetLayout
{
    public const int MinimumHealthyNodes = 3;

    public const string PrimaryAndReadNodeId = "storage-1";

    public const string EncryptedReplicaNodeIdA = "storage-2";

    public const string EncryptedReplicaNodeIdB = "storage-3";

    public static IReadOnlyList<string> EncryptedReplicaNodeIds { get; } =
        [EncryptedReplicaNodeIdA, EncryptedReplicaNodeIdB];

    public static bool IsEncryptedReplicaNode(string nodeId) =>
        EncryptedReplicaNodeIds.Contains(nodeId, StringComparer.Ordinal);

    public static bool CanColocatePrimaryAndRead(string nodeId) =>
        string.Equals(nodeId, PrimaryAndReadNodeId, StringComparison.Ordinal);
}
