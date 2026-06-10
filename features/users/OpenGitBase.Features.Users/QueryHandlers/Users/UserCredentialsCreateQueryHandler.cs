using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.QueryHandlers.Users;

public class UserCredentialsCreateQueryHandler
    : CreateQueryHandlerBase<
        UserCredentialsCreateQuery,
        UserCredentials,
        UserCredentialsId,
        Guid,
        OpenGitBaseDbContext,
        UserCredentialsEntity
    >
{
    public UserCredentialsCreateQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(mapper, contextFactory)
    {
    }
}
