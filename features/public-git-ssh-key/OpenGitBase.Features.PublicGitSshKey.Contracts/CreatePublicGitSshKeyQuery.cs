using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.PublicGitSshKey.Contracts;

namespace OpenGitBase.Features.PublicGitSshKey.Contracts;

public class CreatePublicGitSshKeyQuery
    : CreateQuery<PublicGitSshKeyDto, PublicGitSshKeyId, Guid, CreatePublicGitSshKeyQuery>;
