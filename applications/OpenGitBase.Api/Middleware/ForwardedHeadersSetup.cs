using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using OpenGitBase.Common.Options;

namespace OpenGitBase.Api.Middleware;

internal static class ForwardedHeadersSetup
{
    public static void Configure(
        ForwardedHeadersOptions options,
        InternalNetworkOptions internalNetwork
    )
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // Production traffic crosses two trusted hops (cloudflared, then HAProxy). With the default
        // limit of one entry the client resolved to the cloudflared container, i.e. "internal".
        // Without a limit, entries are consumed right to left only while the hop is a trusted proxy,
        // so the first untrusted address wins and prepended values cannot pose as internal.
        options.ForwardLimit = null;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var cidr in internalNetwork.TrustedProxyNetworks)
        {
            if (TryParseCidr(cidr, out var network))
            {
                options.KnownIPNetworks.Add(network);
            }
        }

        foreach (var address in internalNetwork.TrustedProxyAddresses)
        {
            if (IPAddress.TryParse(address, out var ip))
            {
                options.KnownProxies.Add(ip);
            }
        }
    }

    private static bool TryParseCidr(string cidr, out System.Net.IPNetwork network)
    {
        network = default;
        var parts = cidr.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var prefix))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var prefixLength))
        {
            return false;
        }

        var maxPrefix = prefix.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        if (prefixLength < 0 || prefixLength > maxPrefix)
        {
            return false;
        }

        network = new System.Net.IPNetwork(prefix, prefixLength);
        return true;
    }
}
