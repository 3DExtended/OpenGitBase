using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Repository.Contracts;

public record ProtectedBranchRuleId : Identifier<Guid, ProtectedBranchRuleId>;
