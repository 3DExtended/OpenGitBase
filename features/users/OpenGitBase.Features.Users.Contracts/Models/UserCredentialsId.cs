using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Users.Contracts.Models;

public record UserCredentialsId : Identifier<Guid, UserCredentialsId>;
