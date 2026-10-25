using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>Whether this session may make a security-sensitive change right now.</summary>
public class HasRecentReauthenticationQuery : IQuery<bool, HasRecentReauthenticationQuery>
{
    public UserId UserId { get; set; }

    /// <summary>The session token's <c>jti</c>.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>When the session token was issued (<c>ogb_iat_ms</c>).</summary>
    public DateTimeOffset SessionIssuedAt { get; set; }
}
