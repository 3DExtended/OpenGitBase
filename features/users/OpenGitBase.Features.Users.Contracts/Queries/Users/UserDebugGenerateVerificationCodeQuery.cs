using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserDebugGenerateVerificationCodeQuery
    : IQuery<UserDebugVerificationCode, UserDebugGenerateVerificationCodeQuery>
{
    public UserId UserId { get; set; } = null!;
}
