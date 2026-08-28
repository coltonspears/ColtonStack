using Microsoft.Data.Sqlite;

namespace ColtonStack.Server.Infrastructure;

/// <summary>Creates pre-opened SQLite connections — one per operation, opened and disposed asynchronously.</summary>
public interface IDbConnectionFactory
{
    Task<SqliteConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken);
}
