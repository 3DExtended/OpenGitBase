#pragma warning disable SA1402 // File may only contain a single type
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace OpenGitBase.Common.Tests.Testing;

/// <summary>
/// Reproduces a commit whose acknowledgement never reaches the client: the insert into the armed table commits, then
/// the client sees a transient error, as Npgsql reports a COMMIT that outlived its command timeout (prod, 2026-10-18).
/// The insert commits either with its transaction or, when EF sends it as a lone statement, on its own.
/// <see cref="Configure"/> pairs it with a strategy that retries like production's EnableRetryOnFailure.
/// </summary>
public sealed class LostCommitSimulator : DbTransactionInterceptor, IDbCommandInterceptor
{
    private string? _armedInsert;
    private bool _armedInsertPending;

    public int LostCommits { get; private set; }

    public void Configure(DbContextOptionsBuilder options) =>
        options
            .UseSqlite(sqlite => sqlite.ExecutionStrategy(dependencies => new RetryOnSimulatedLostCommitStrategy(dependencies)))
            .AddInterceptors(this);

    /// <summary>Loses the acknowledgement of the next commit that inserts into <paramref name="table"/>.</summary>
    public void LoseNextCommitInserting(string table) => _armedInsert = $"INSERT INTO \"{table}\"";

    public async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default
    )
    {
        if (!InsertsIntoArmedTable(command))
        {
            return result;
        }

        if (command.Transaction is not null)
        {
            _armedInsertPending = true;
            return result;
        }

        // A lone statement commits once its reader is done with it.
        await result.DisposeAsync();
        throw LoseCommit();
    }

    public ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default
    )
    {
        if (!InsertsIntoArmedTable(command))
        {
            return ValueTask.FromResult(result);
        }

        if (command.Transaction is null)
        {
            throw LoseCommit();
        }

        _armedInsertPending = true;
        return ValueTask.FromResult(result);
    }

    public override async Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        await base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
        if (_armedInsertPending)
        {
            throw LoseCommit();
        }
    }

    public override Task TransactionRolledBackAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        _armedInsertPending = false;
        return base.TransactionRolledBackAsync(transaction, eventData, cancellationToken);
    }

    private bool InsertsIntoArmedTable(DbCommand command) =>
        _armedInsert is not null && command.CommandText.Contains(_armedInsert, StringComparison.Ordinal);

    private SimulatedLostCommitException LoseCommit()
    {
        _armedInsert = null;
        _armedInsertPending = false;
        LostCommits++;
        return new SimulatedLostCommitException();
    }
}

public sealed class SimulatedLostCommitException : Exception
{
    public SimulatedLostCommitException()
        : base("The commit's acknowledgement was lost; the transaction did commit.")
    {
    }
}

/// <summary>Retries a lost commit acknowledgement the way Npgsql's EnableRetryOnFailure retries a timed-out COMMIT.</summary>
public sealed class RetryOnSimulatedLostCommitStrategy : ExecutionStrategy
{
    public RetryOnSimulatedLostCommitStrategy(ExecutionStrategyDependencies dependencies)
        : base(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(10))
    {
    }

    protected override bool ShouldRetryOn(Exception exception) => exception is SimulatedLostCommitException;
}
