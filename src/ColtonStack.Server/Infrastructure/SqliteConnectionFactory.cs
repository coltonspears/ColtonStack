using Microsoft.Data.Sqlite;

namespace ColtonStack.Server.Infrastructure;

/// <summary>SQLite implementation: one short-lived connection per operation.</summary>
public sealed class SqliteConnectionFactory(string databasePath) : IDbConnectionFactory
{
    public async Task<SqliteConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
            }.ToString());

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
