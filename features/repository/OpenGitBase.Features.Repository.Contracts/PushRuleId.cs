using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Repository.Contracts;

public record PushRuleId : Identifier<Guid, PushRuleId>;
