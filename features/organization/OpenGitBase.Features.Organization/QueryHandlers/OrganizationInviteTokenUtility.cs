using System.Security.Cryptography;
using OpenGitBase.Common.Services;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

internal static class OrganizationInviteTokenUtility
{
    internal static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    internal static OrganizationInviteEntity? FindByToken(
        IEnumerable<OrganizationInviteEntity> invites,
        IPasswordHasherService passwordHasherService,
        string token
    )
    {
        foreach (var invite in invites)
        {
            if (passwordHasherService.VerifyPassword(invite.TokenHash, token))
            {
                return invite;
            }
        }

        return null;
    }

    internal static OrganizationInviteStatus ResolveStatus(OrganizationInviteEntity invite, DateTimeOffset utcNow)
    {
        if (invite.Status == OrganizationInviteStatus.Pending && invite.ExpiresAt <= utcNow)
        {
            return OrganizationInviteStatus.Expired;
        }

        return invite.Status;
    }

    internal static string RedactEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
        {
            return "***";
        }

        var local = email[..atIndex];
        var domain = email[(atIndex + 1)..];
        var visible = local.Length <= 2 ? local[..1] : local[..2];
        return $"{visible}***@{domain}";
    }
}
