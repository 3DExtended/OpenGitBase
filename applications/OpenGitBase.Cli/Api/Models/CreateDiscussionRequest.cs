namespace OpenGitBase.Cli.Api.Models;

public sealed class CreateDiscussionRequest
{
    public required string Title { get; init; }

    public string? Body { get; init; }
}
