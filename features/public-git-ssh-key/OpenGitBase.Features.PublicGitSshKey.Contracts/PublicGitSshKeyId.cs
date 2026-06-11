using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.PublicGitSshKey.Contracts;

public record PublicGitSshKeyId : Identifier<Guid, PublicGitSshKeyId>;
