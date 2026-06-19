namespace OpenGitBase.Features.GitAccessToken.Contracts;

public static class GitAccessTokenScopes
{
    public const string Read = "read";

    public const string Write = "write";

    public static bool IsValid(string? scope) =>
        scope is Read or Write;
}
