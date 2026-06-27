#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1402 // File may only contain a single type
using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public class CreateProtectedBranchRuleQuery
    : IQuery<ProtectedBranchRuleId, CreateProtectedBranchRuleQuery>
{
    public ProtectedBranchRuleDto ModelToCreate { get; set; } = default!;
}

public class GetProtectedBranchRuleQuery : IQuery<ProtectedBranchRuleDto, GetProtectedBranchRuleQuery>
{
    public ProtectedBranchRuleId ModelId { get; set; } = default!;
}

public class ListProtectedBranchRulesQuery
    : IQuery<IReadOnlyList<ProtectedBranchRuleDto>, ListProtectedBranchRulesQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;
}

public class UpdateProtectedBranchRuleQuery
    : IQuery<Unit, UpdateProtectedBranchRuleQuery>
{
    public ProtectedBranchRuleDto UpdatedModel { get; set; } = default!;
}

public class DeleteProtectedBranchRuleQuery
    : IQuery<Unit, DeleteProtectedBranchRuleQuery>
{
    public ProtectedBranchRuleId Id { get; set; } = default!;
}
