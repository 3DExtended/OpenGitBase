using Microsoft.Data.Sqlite;

namespace OpenGitBase.Common.Tests.Testing;

public static class SqliteTestConnection
{
    private static readonly Lock OpenGate = new();

    public static SqliteConnection OpenInMemory()
    {
        lock (OpenGate)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }
    }
}
