using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Features.Organization.Contracts;

public class UpdateOrganizationMemberQuery
    : UpdateCommand<
        OrganizationMemberDto,
        OrganizationMemberId,
        Guid,
        UpdateOrganizationMemberQuery
    >;
