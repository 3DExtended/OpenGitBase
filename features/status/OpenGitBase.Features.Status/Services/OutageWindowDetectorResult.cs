namespace OpenGitBase.Features.Status.Services;

public sealed class OutageWindowDetectorResult
{
    public List<OutageWindowRecord> Upserts { get; set; } = [];

    public List<Guid> Deletes { get; set; } = [];

    public List<string> Logs { get; set; } = [];
}
