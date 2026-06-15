using System.Net;
using OpenGitBase.Common.Security;

namespace OpenGitBase.Api.Tests.Middleware;

public class InternalNetworkMiddlewareTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.0.0.5", true)]
    [InlineData("172.16.0.2", true)]
    [InlineData("192.168.1.20", true)]
    [InlineData("8.8.8.8", false)]
    public void IsInternal_ClassifiesNetworks(string address, bool expected)
    {
        Assert.Equal(expected, InternalNetworkAddress.IsInternal(IPAddress.Parse(address)));
    }
}
