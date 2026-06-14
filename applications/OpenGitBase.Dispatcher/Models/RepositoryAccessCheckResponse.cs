namespace OpenGitBase.Dispatcher.Models;

public sealed class RepositoryAccessCheckResponse
{
    public bool Allowed { get; init; }
    public string? Reason { get; init; }
}
