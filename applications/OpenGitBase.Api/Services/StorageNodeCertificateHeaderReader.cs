using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;

namespace OpenGitBase.Api.Services;

public static class StorageNodeCertificateHeaderReader
{
    public const string ThumbprintHeaderName = "X-Storage-Node-Certificate-Thumbprint";

    public static string? ReadThumbprint(HttpRequest request)
    {
        if (request.Headers.TryGetValue(ThumbprintHeaderName, out var headerValues))
        {
            var headerValue = headerValues.ToString();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                return headerValue;
            }
        }

        var certificate = request.HttpContext.Connection.ClientCertificate;
        if (certificate is null)
        {
            return null;
        }

        return certificate.GetCertHashString(HashAlgorithmName.SHA256);
    }
}
