using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public static class StorageNodeCapacity
{
    /// <summary>
    /// Returns false when <paramref name="node"/> has a positive <see cref="StorageNodeDto.MaxBytes"/>
    /// and <paramref name="additionalBytes"/> would exceed remaining capacity.
    /// </summary>
    public static bool HasCapacity(StorageNodeDto node, long additionalBytes)
    {
        if (node.MaxBytes <= 0)
        {
            return true;
        }

        return node.UsedBytes + additionalBytes <= node.MaxBytes;
    }

    public static long RemainingBytes(StorageNodeDto node) =>
        node.MaxBytes <= 0 ? long.MaxValue : Math.Max(0, node.MaxBytes - node.UsedBytes);

    public static long ComputeUsedBytes(long totalBytesAvailable, long freeBytesAvailable) =>
        Math.Max(0, totalBytesAvailable - freeBytesAvailable);
}
