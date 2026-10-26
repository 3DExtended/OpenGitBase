using System.Net;
using Microsoft.AspNetCore.Http;
using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Tests.Services;

public class HttpRemoteIpAddressAccessorTests
{
    [Fact]
    public void RemoteIpAddress_IsTheRequestsClientAddress()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.23");

        var accessor = new HttpRemoteIpAddressAccessor(new HttpContextAccessor { HttpContext = context });

        Assert.Equal("198.51.100.23", accessor.RemoteIpAddress);
    }

    [Fact]
    public void RemoteIpAddress_OutsideARequest_IsNull()
    {
        var accessor = new HttpRemoteIpAddressAccessor(new HttpContextAccessor());

        Assert.Null(accessor.RemoteIpAddress);
    }
}
