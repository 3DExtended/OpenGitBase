using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.PublicGitSshKey.Contracts;

public class GetUserIdBySshKeyFingerprintQuery : IQuery<UserId, GetUserIdBySshKeyFingerprintQuery>
{
    public string Fingerprint { get; set; } = string.Empty;
}
