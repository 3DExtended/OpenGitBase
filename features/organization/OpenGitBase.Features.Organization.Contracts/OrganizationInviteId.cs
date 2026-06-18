using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Organization.Contracts;

public record OrganizationInviteId : Identifier<Guid, OrganizationInviteId>;
