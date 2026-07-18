using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class RepublishKafkaJobWakesQuery
    : IQuery<RepublishKafkaJobWakesResult, RepublishKafkaJobWakesQuery>
{
}
