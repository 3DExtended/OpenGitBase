namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentDiffFilePayload
{
    public string? OldPath { get; init; }

    public string? NewPath { get; init; }

    public string Status { get; init; } = string.Empty;

    public bool IsBinary { get; init; }

    public IReadOnlyList<StorageContentDiffHunkPayload> Hunks { get; init; } = [];
}
