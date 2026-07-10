using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Status.Contracts;

public record FleetComponentId : Identifier<Guid, FleetComponentId>;
