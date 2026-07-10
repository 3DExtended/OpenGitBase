using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Tests.Services;

public class StorageNodeCertificateHeaderReaderTests
{
    [Fact]
    public void ReadThumbprint_WhenHeaderPresentWithoutClientCertificate_FromInternalNetwork_ReturnsNormalizedThumbprint()
    {
        var context = new DefaultHttpContext
        {
            Connection =
            {
                RemoteIpAddress = IPAddress.Parse("172.18.0.5"),
            },
        };
        context.Request.Headers[StorageNodeCertificateHeaderReader.ThumbprintHeaderName] =
            "d1c3afde7426834ae75e6e0237425137ee045ed9663ea095b04700e9c2e83973";

        var thumbprint = StorageNodeCertificateHeaderReader.ReadThumbprint(context.Request);

        Assert.Equal(
            "D1C3AFDE7426834AE75E6E0237425137EE045ED9663EA095B04700E9C2E83973",
            thumbprint
        );
    }

    [Fact]
    public void ReadThumbprint_WhenHeaderPresentWithoutClientCertificate_FromExternalNetwork_ReturnsNull()
    {
        var context = new DefaultHttpContext
        {
            Connection =
            {
                RemoteIpAddress = IPAddress.Parse("203.0.113.10"),
            },
        };
        context.Request.Headers[StorageNodeCertificateHeaderReader.ThumbprintHeaderName] =
            "deadbeef".PadRight(64, '0');

        var thumbprint = StorageNodeCertificateHeaderReader.ReadThumbprint(context.Request);

        Assert.Null(thumbprint);
    }

    [Fact]
    public void ReadThumbprint_WhenClientCertificatePresent_ReturnsSha256Thumbprint()
    {
        using var certificate = CreateSelfSignedCertificate();
        var context = new DefaultHttpContext
        {
            Connection =
            {
                ClientCertificate = certificate,
                RemoteIpAddress = IPAddress.Loopback,
            },
        };
        context.Request.Headers[StorageNodeCertificateHeaderReader.ThumbprintHeaderName] =
            "spoofed-thumbprint";

        var thumbprint = StorageNodeCertificateHeaderReader.ReadThumbprint(context.Request);

        Assert.Equal(
            certificate.GetCertHashString(HashAlgorithmName.SHA256),
            thumbprint
        );
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=storage-test",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
