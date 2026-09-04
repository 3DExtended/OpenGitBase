using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class GetFleetDriftReportQuery
    : IQuery<FleetDriftReportDto, GetFleetDriftReportQuery>;
