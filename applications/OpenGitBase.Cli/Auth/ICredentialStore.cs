namespace OpenGitBase.Cli.Auth;

public interface ICredentialStore
{
    void SaveToken(string host, string token);

    string? GetToken(string host);

    void DeleteToken(string host);

    bool HasToken(string host);
}
