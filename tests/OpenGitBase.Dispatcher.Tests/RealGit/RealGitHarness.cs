namespace OpenGitBase.Dispatcher.Tests.RealGit;

/// <summary>
/// A real git client talking Smart HTTP to the dispatcher on real Kestrel, which proxies to
/// <see cref="GitHttpBackendHost"/>: the same git-http-backend storage runs behind nginx and fcgiwrap.
/// </summary>
internal sealed class RealGitHarness : IAsyncDisposable
{
    public const string RepositoryPath = "alice/demo";

    private const string AccessToken = "ogb_test_token";

    private RealGitHarness(string root, GitHttpBackendHost storage, DispatcherHttpHost dispatcher)
    {
        Root = root;
        Storage = storage;
        Dispatcher = dispatcher;
        Git = new GitCli(Path.Combine(root, "home"), AccessToken);
    }

    public string Root { get; }

    /// <summary>The bare repository storage serves, as storage-1 holds it under /srv/git.</summary>
    public string BareRepository => Path.Combine(Root, "srv", "demo-id.git");

    public string RemoteUrl => $"http://127.0.0.1:{Dispatcher.Port}/{RepositoryPath}.git";

    public GitCli Git { get; }

    public GitHttpBackendHost Storage { get; }

    public DispatcherHttpHost Dispatcher { get; }

    public static async Task<RealGitHarness> StartAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "ogb-real-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "srv"));
        Directory.CreateDirectory(Path.Combine(root, "home"));

        var storage = await GitHttpBackendHost.StartAsync(Path.Combine(root, "srv"), Path.Combine(root, "home"));
        var dispatcher = await DispatcherHttpHost.StartAsync(storage.Port, "/srv/git/demo-id.git");
        return new RealGitHarness(root, storage, dispatcher);
    }

    public async ValueTask DisposeAsync()
    {
        await Dispatcher.DisposeAsync();
        await Storage.DisposeAsync();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory must not fail the test.
        }
        catch (UnauthorizedAccessException)
        {
            // Git writes read-only pack files; same as above.
        }
    }
}
