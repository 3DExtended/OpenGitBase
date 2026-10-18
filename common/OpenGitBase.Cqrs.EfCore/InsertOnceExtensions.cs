namespace OpenGitBase.Cqrs.EfCore;

public static class InsertOnceExtensions
{
    /// <summary>
    /// Runs <paramref name="insert"/> in its own transaction under the context's execution strategy, so a retry replays
    /// the whole write instead of a bare SaveChanges. A retried COMMIT may have landed although its acknowledgement was
    /// lost (prod, 2026-10-18: Npgsql's EnableRetryOnFailure replayed such a COMMIT into a duplicate key and a 500).
    /// When the replay then collides with the row the earlier attempt committed, <paramref name="isInserted"/> confirms
    /// that row and the earlier attempt's result is returned: the row is saved once and reported as saved.
    /// </summary>
    /// <param name="insert">Adds the new rows and saves them; it gives them the same key on every attempt.</param>
    /// <param name="isInserted">Whether the row <paramref name="insert"/> adds is committed.</param>
    public static Task<TResult> InsertOnceAsync<TResult>(
        this DbContext context,
        Func<CancellationToken, Task<TResult>> insert,
        Func<CancellationToken, Task<bool>> isInserted,
        CancellationToken cancellationToken
    )
    {
        var commitAttempted = false;
        TResult committedResult = default!;
        return context
            .Database.CreateExecutionStrategy()
            .ExecuteAsync(async () =>
            {
                context.ChangeTracker.Clear();

                // Replaying an insert whose commit landed could reject it as a duplicate of itself.
                if (commitAttempted && await isInserted(cancellationToken).ConfigureAwait(false))
                {
                    return committedResult;
                }

                await using var transaction = await context
                    .Database.BeginTransactionAsync(cancellationToken)
                    .ConfigureAwait(false);
                TResult result;
                try
                {
                    result = await insert(cancellationToken).ConfigureAwait(false);
                }
                catch (DbUpdateException) when (commitAttempted)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);

                    // The replay's rows never committed; left tracked they would shadow the committed ones on reload.
                    context.ChangeTracker.Clear();
                    if (await isInserted(cancellationToken).ConfigureAwait(false))
                    {
                        return committedResult;
                    }

                    throw;
                }

                committedResult = result;
                commitAttempted = true;
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return result;
            });
    }
}
