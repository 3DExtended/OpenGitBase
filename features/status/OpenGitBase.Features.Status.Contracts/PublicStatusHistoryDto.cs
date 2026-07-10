namespace OpenGitBase.Features.Status.Contracts;

public sealed class PublicStatusHistoryDto
{
    public List<PublicStatusHistoryGroupSeriesDto> Groups { get; set; } = [];

    public List<PublicStatusHistoryDayDto> Overall { get; set; } = [];

    public List<PublicStatusHistoryDayDto> OverallStateMix { get; set; } = [];
}
