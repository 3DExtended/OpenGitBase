using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Status.Contracts;

public sealed class ListAdminStatusOutageWindowsQuery
    : IQuery<List<AdminStatusOutageWindowDto>, ListAdminStatusOutageWindowsQuery>;
