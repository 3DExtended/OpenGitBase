using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

/// <summary>
/// LFS figures over every repository for the admin dashboard: what is stored, and how many copies the nodes still
/// miss (the same count <see cref="GetLfsCopyStatusQuery"/> gives per repository).
/// </summary>
public sealed class GetLfsFleetTotalsQuery : IQuery<LfsFleetTotalsDto, GetLfsFleetTotalsQuery>;
