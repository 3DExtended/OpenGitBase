using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace OpenGitBase.Common.Tests.Testing;

/// <summary>Fails every command whose SQL contains <c>sqlFragment</c>, e.g. to break a side effect after a commit.</summary>
public sealed class FailingCommandInterceptor : DbCommandInterceptor
{
    private readonly string _sqlFragment;

    public FailingCommandInterceptor(string sqlFragment)
    {
        _sqlFragment = sqlFragment;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default
    )
    {
        ThrowIfMatched(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        ThrowIfMatched(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void ThrowIfMatched(DbCommand command)
    {
        if (command.CommandText.Contains(_sqlFragment, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Simulated failure of: {_sqlFragment}");
        }
    }
}
