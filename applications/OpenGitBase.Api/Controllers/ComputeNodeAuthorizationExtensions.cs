using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Services;
using OpenGitBase.Features.ComputeNode.Entities;

namespace OpenGitBase.Api.Controllers;

internal static class ComputeNodeAuthorizationExtensions
{
    public static string? GetBearerToken(this HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out var header))
        {
            return null;
        }

        var value = header.ToString();
        const string prefix = "Bearer ";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value[prefix.Length..].Trim();
    }

    public static async Task<ComputeNodeEntity?> AuthenticateComputeNodeAsync(
        this ControllerBase controller,
        ComputeNodeIdentityService identityService,
        CancellationToken cancellationToken
    )
    {
        return await identityService
            .AuthenticateAsync(controller.Request.GetBearerToken(), cancellationToken)
            .ConfigureAwait(false);
    }
}
