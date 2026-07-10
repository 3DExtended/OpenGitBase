using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using OpenGitBase.Common.Security;

namespace OpenGitBase.Api.Services;

public static class StorageNodeCertificateHeaderReader
{
    public const string ThumbprintHeaderName = "X-Storage-Node-Certificate-Thumbprint";

    public static string? ReadThumbprint(HttpRequest request)
    {
        var certificate = request.HttpContext.Connection.ClientCertificate;
        if (certificate is not null)
        {
            return certificate.GetCertHashString(HashAlgorithmName.SHA256);
        }

        var remoteIp = request.HttpContext.Connection.RemoteIpAddress;
        if (remoteIp is null || !InternalNetworkAddress.IsInternal(remoteIp))
        {
            return null;
        }

        if (!request.Headers.TryGetValue(ThumbprintHeaderName, out var headerValues))
        {
            return null;
        }

        var normalized = NodeCertificateThumbprint.Normalize(headerValues.ToString());
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
