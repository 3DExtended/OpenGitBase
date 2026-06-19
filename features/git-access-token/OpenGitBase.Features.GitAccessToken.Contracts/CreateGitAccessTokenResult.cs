namespace OpenGitBase.Features.GitAccessToken.Contracts;

public sealed class CreateGitAccessTokenResult
{
    public GitAccessTokenId Id { get; set; } = default!;

    public string Token { get; set; } = string.Empty;

    public GitAccessTokenDto Metadata { get; set; } = default!;
}
