namespace OpenGitBase.Features.Status.Contracts;

public sealed class PublicStatusHistoryGroupSeriesDto
{
    public StatusComponentGroup Group { get; set; }

    public List<PublicStatusHistoryDayDto> Days { get; set; } = [];
}
