using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Tests.Services;

public class StorageNodeCertificateHeaderReaderTests
{
    [Fact]
    public void ReadThumbprint_WhenHeaderPresentWithoutClientCertificate_ReturnsNull()
    {
        var context = new DefaultHttpContext();
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
