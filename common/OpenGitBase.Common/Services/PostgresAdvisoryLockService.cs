using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;

namespace OpenGitBase.Common.Services;

public static class PostgresAdvisoryLockService
{
    public static async Task<bool> TryAcquireAsync(
        OpenGitBaseDbContext context,
        long key,
        CancellationToken cancellationToken
    )
    {
        // pg_try_advisory_lock is session-scoped: the lock is held by whichever physical
        // connection issues it. We must keep that exact connection open (not return it to
        // the pool) until ReleaseAsync unlocks on it, otherwise a later Open can hand back a
        // different pooled connection and the lock leaks forever on the original session.
        await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@key)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "key";
        parameter.Value = key;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var acquired = result is true;
        if (!acquired)
        {
            await context.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        return acquired;
    }

    /// <summary>
    /// Waits until the lock is free and takes it. Like <see cref="TryAcquireAsync"/>, the lock is
    /// held on the context's connection, which stays open until <see cref="ReleaseAsync"/>. The
    /// context's command timeout bounds the wait.
    /// </summary>
    public static async Task AcquireAsync(
        OpenGitBaseDbContext context,
        long key,
        CancellationToken cancellationToken
    )
    {
        await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await context.Database
                .ExecuteSqlAsync($"SELECT pg_advisory_lock({key})", cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await context.Database.CloseConnectionAsync().ConfigureAwait(false);
            throw;
        }
    }

    public static async Task ReleaseAsync(
        OpenGitBaseDbContext context,
        long key,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT pg_advisory_unlock(@key)";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "key";
            parameter.Value = key;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await context.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }
}
