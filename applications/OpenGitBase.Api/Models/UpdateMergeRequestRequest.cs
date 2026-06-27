namespace OpenGitBase.Api.Models;

public sealed class UpdateMergeRequestRequest
{
    public string? Title { get; set; }
    public string? Body { get; set; }
    public bool ClearBody { get; set; }
}
