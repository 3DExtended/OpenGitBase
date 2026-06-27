namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentDiffHunkPayload
{
    public int OldStart { get; init; }

    public int OldLines { get; init; }

    public int NewStart { get; init; }

    public int NewLines { get; init; }

    public IReadOnlyList<StorageContentDiffLinePayload> Lines { get; init; } = [];
}
