using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.PublicGitSshKey.Contracts;

namespace OpenGitBase.Features.PublicGitSshKey.Contracts;

public class DeletePublicGitSshKeyQuery
    : DeleteCommand<PublicGitSshKeyDto, PublicGitSshKeyId, Guid, DeletePublicGitSshKeyQuery>;
