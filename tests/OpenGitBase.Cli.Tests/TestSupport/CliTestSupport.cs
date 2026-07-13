using OpenGitBase.Cli.Auth;

namespace OpenGitBase.Cli.Tests.TestSupport;

public static class CliTestSupport
{
    public static CliServiceOverrides CreateOverrides(
        StubHttpMessageHandler handler,
        string host,
        string? workingDirectory = null)
    {
        var credentialStore = new InMemoryCredentialStore();
        credentialStore.SaveToken(host, AuthCommandTestsHelpers.CreateJwt("alice"));
        return new CliServiceOverrides
        {
            CredentialStore = credentialStore,
            HttpClient = new HttpClient(handler),
            WorkingDirectory = workingDirectory,
        };
    }

    public static string CreateGitRepoWithOrigin(string host, string owner, string slug)
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ogb-git-" + Guid.NewGuid().ToString("N")));
        var gitDir = Path.Combine(dir.FullName, ".git");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(
            Path.Combine(gitDir, "config"),
            $"""
            [remote "origin"]
                url = {host}/{owner}/{slug}.git
                fetch = +refs/heads/*:refs/remotes/origin/*
            """);
        return dir.FullName;
    }
}
