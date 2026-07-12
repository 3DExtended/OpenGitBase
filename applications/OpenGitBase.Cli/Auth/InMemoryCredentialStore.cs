namespace OpenGitBase.Cli.Auth;

public sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _tokens = new(StringComparer.OrdinalIgnoreCase);

    public void SaveToken(string host, string token) => _tokens[host] = token;

    public string? GetToken(string host) =>
        _tokens.TryGetValue(host, out var token) ? token : null;

    public void DeleteToken(string host) => _tokens.Remove(host);

    public bool HasToken(string host) => _tokens.ContainsKey(host);
}
