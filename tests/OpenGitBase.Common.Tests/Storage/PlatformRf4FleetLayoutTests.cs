using OpenGitBase.Common.Storage;

namespace OpenGitBase.Common.Tests.Storage;

public class PlatformRf4FleetLayoutTests
{
    [Fact]
    public void MinimumHealthyNodes_IsThree()
    {
        Assert.Equal(3, PlatformRf4FleetLayout.MinimumHealthyNodes);
    }

    [Fact]
    public void EncryptedReplicaNodeIds_AreDistinctFromPrimaryAndReadNode()
    {
        Assert.DoesNotContain(
            PlatformRf4FleetLayout.PrimaryAndReadNodeId,
            PlatformRf4FleetLayout.EncryptedReplicaNodeIds
        );
        Assert.Equal(2, PlatformRf4FleetLayout.EncryptedReplicaNodeIds.Count);
    }

    [Theory]
    [InlineData("storage-2", true)]
    [InlineData("storage-3", true)]
    [InlineData("storage-1", false)]
    public void IsEncryptedReplicaNode_ClassifiesComposeNodes(string nodeId, bool expected)
    {
        Assert.Equal(expected, PlatformRf4FleetLayout.IsEncryptedReplicaNode(nodeId));
    }

    [Fact]
    public void CanColocatePrimaryAndRead_OnlyOnStorageOne()
    {
        Assert.True(PlatformRf4FleetLayout.CanColocatePrimaryAndRead("storage-1"));
        Assert.False(PlatformRf4FleetLayout.CanColocatePrimaryAndRead("storage-2"));
    }
}
