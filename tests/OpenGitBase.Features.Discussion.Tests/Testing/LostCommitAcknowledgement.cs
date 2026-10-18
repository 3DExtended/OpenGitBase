#pragma warning disable SA1402 // File may only contain a single type
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace OpenGitBase.Features.Discussion.Tests.Testing;

/// <summary>
/// Reproduces a commit whose acknowledgement never reaches the client: the transaction that inserts a discussion
/// comment commits, then the client sees a transient error, as Npgsql reports a COMMIT that outlived its command
/// timeout. <see cref="Configure"/> pairs it with a strategy that retries like production's EnableRetryOnFailure.
/// </summary>
public sealed class LostCommitAcknowledgementInterceptor : DbTransactionInterceptor, IDbCommandInterceptor
{
    private bool _armed;
    private bool _commentInsertPending;

    public int LostAcknowledgements { get; private set; }

    public void Configure(DbContextOptionsBuilder options) =>
        options
            .UseSqlite(sqlite => sqlite.ExecutionStrategy(dependencies => new RetryOnLostCommitExecutionStrategy(dependencies)))
            .AddInterceptors(this);

    /// <summary>Loses the acknowledgement of the next commit that inserts a discussion comment.</summary>
    public void LoseNextCommentCommit() => _armed = true;

    public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default
    )
    {
        if (_armed && command.CommandText.Contains("INSERT INTO \"discussion_comments\"", StringComparison.Ordinal))
        {
            _commentInsertPending = true;
        }

        return ValueTask.FromResult(result);
    }

    public override async Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        await base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
        if (!_commentInsertPending)
        {
            return;
        }

        _armed = false;
        _commentInsertPending = false;
        LostAcknowledgements++;
        throw new LostCommitAcknowledgementException();
    }

    public override Task TransactionRolledBackAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        _commentInsertPending = false;
        return base.TransactionRolledBackAsync(transaction, eventData, cancellationToken);
    }
}

public sealed class LostCommitAcknowledgementException : Exception
{
    public LostCommitAcknowledgementException()
        : base("The commit's acknowledgement was lost; the transaction did commit.")
    {
    }
}

/// <summary>Retries a lost commit acknowledgement the way Npgsql's EnableRetryOnFailure retries a timed-out COMMIT.</summary>
public sealed class RetryOnLostCommitExecutionStrategy : ExecutionStrategy
{
    public RetryOnLostCommitExecutionStrategy(ExecutionStrategyDependencies dependencies)
        : base(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(10))
    {
    }

    protected override bool ShouldRetryOn(Exception exception) => exception is LostCommitAcknowledgementException;
}
