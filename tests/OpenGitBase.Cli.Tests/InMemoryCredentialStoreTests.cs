using OpenGitBase.Cli.Auth;

namespace OpenGitBase.Cli.Tests;

public sealed class InMemoryCredentialStoreTests
{
    [Fact]
    public void Save_get_delete_round_trip()
    {
        var store = new InMemoryCredentialStore();
        const string host = "https://example.com";

        store.SaveToken(host, "token-1");
        Assert.True(store.HasToken(host));
        Assert.Equal("token-1", store.GetToken(host));

        store.DeleteToken(host);
        Assert.False(store.HasToken(host));
        Assert.Null(store.GetToken(host));
    }
}
