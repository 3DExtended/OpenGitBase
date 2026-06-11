using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.PublicGitSshKey.Contracts;

namespace OpenGitBase.Features.PublicGitSshKey.Contracts;

public class GetPublicGitSshKeyQuery
    : SingleModelQuery<PublicGitSshKeyDto, PublicGitSshKeyId, Guid, GetPublicGitSshKeyQuery>;
