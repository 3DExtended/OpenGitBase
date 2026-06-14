using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.PublicGitSshKey.Contracts;

public class GetPublicGitSshKeyByFingerprintQuery
    : IQuery<PublicGitSshKeyDto, GetPublicGitSshKeyByFingerprintQuery>
{
    public string Fingerprint { get; set; } = string.Empty;
}
