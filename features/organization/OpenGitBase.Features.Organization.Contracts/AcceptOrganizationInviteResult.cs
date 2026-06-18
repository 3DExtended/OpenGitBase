namespace OpenGitBase.Features.Organization.Contracts;

public enum AcceptOrganizationInviteResult
{
    Accepted = 0,
    NotFound = 1,
    Expired = 2,
    EmailMismatch = 3,
    AlreadyMember = 4,
}
