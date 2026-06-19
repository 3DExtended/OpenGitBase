using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.GitAccessToken.Contracts;

public record GitAccessTokenId : Identifier<Guid, GitAccessTokenId>;
