using ColtonStack.Server.Data;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Server.Infrastructure;

/// <summary>
/// Creates the SQLite schema on startup and seeds a believable workspace the first time the
/// server runs. Runs as a hosted service — the Generic Host manages its lifetime. Extensions
/// add their own tables through <see cref="ISchemaContributor"/>.
/// </summary>
public sealed partial class SqliteDatabaseInitializer(
    IDbConnectionFactory connectionFactory,
    IEnumerable<ISchemaContributor> schemaContributors,
    ILogger<SqliteDatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        DapperConfig.Register();

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await CreateSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        foreach (var contributor in schemaContributors)
        {
            await ExecuteAsync(connection, contributor.Schema, cancellationToken).ConfigureAwait(false);
            ExtensionSchemaApplied(contributor.Name);
        }

        await SeedIfEmptyAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task CreateSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string Schema = """
            CREATE TABLE IF NOT EXISTS Users (
                Id          INTEGER PRIMARY KEY,
                DisplayName TEXT NOT NULL,
                AvatarColor TEXT NOT NULL,
                IsSelf      INTEGER NOT NULL DEFAULT 0);

            CREATE TABLE IF NOT EXISTS Channels (
                Id    INTEGER PRIMARY KEY,
                Name  TEXT NOT NULL UNIQUE,
                Topic TEXT NOT NULL DEFAULT '');

            CREATE TABLE IF NOT EXISTS Messages (
                Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                ChannelId      INTEGER NOT NULL REFERENCES Channels(Id),
                UserId         INTEGER NOT NULL REFERENCES Users(Id),
                Text           TEXT NOT NULL,
                CreatedAtUtc   TEXT NOT NULL,
                AttachmentKind TEXT,
                AttachmentJson TEXT);

            CREATE INDEX IF NOT EXISTS IX_Messages_ChannelId_Id ON Messages(ChannelId, Id);

            CREATE TABLE IF NOT EXISTS Webhooks (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                Url          TEXT NOT NULL,
                Secret       TEXT,
                IsActive     INTEGER NOT NULL DEFAULT 1,
                CreatedAtUtc TEXT NOT NULL);

            CREATE TABLE IF NOT EXISTS AuditLog (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                EntityType   TEXT NOT NULL,
                EntityId     INTEGER NOT NULL,
                Action       TEXT NOT NULL,
                Actor        TEXT NOT NULL,
                TimestampUtc TEXT NOT NULL,
                PayloadJson  TEXT);

            CREATE TABLE IF NOT EXISTS Settings (
                Key          TEXT PRIMARY KEY,
                Value        TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL);
            """;

        await ExecuteAsync(connection, Schema, cancellationToken).ConfigureAwait(false);

        // Databases created before attachments existed get the two columns added in place.
        await EnsureColumnAsync(connection, "Messages", "AttachmentKind", "TEXT", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "Messages", "AttachmentJson", "TEXT", cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureColumnAsync(SqliteConnection connection, string table, string column, string type, CancellationToken cancellationToken)
    {
        var columns = await connection.QueryAsync<string>($"SELECT name FROM pragma_table_info('{table}')").ConfigureAwait(false);
        if (!columns.Contains(column, StringComparer.OrdinalIgnoreCase))
        {
            await ExecuteAsync(connection, $"ALTER TABLE {table} ADD COLUMN {column} {type}", cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SeedIfEmptyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var existingUsers = await connection.GetAllAsync<UserRow>().ConfigureAwait(false);
        if (existingUsers.Any())
        {
            DatabaseAlreadySeeded();
            return;
        }

        SeedingWorkspace();
        var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            // Dapper.Contrib writes each generated id back onto the row, so the maps below connect
            // seed data to real keys without a single INSERT or subquery being written by hand.
            var userIdsByName = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var user in SeedData.Users)
            {
                var row = new UserRow { DisplayName = user.Name, AvatarColor = user.Color, IsSelf = user.IsSelf };
                await connection.InsertAsync(row, transaction).ConfigureAwait(false);
                userIdsByName[user.Name] = row.Id;
            }

            var channelIdsBySeedId = new Dictionary<long, long>();
            foreach (var channel in SeedData.Channels)
            {
                var row = new ChannelRow { Name = channel.Name, Topic = channel.Topic };
                await connection.InsertAsync(row, transaction).ConfigureAwait(false);
                channelIdsBySeedId[channel.Id] = row.Id;
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var message in SeedData.Messages)
            {
                var row = new MessageRow
                {
                    ChannelId = channelIdsBySeedId[message.Channel],
                    UserId = userIdsByName[message.Author],
                    Text = message.Text,
                    CreatedAtUtc = now.AddMinutes(-message.MinutesAgo),
                };
                await connection.InsertAsync(row, transaction).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        SeededWorkspace(SeedData.Users.Length, SeedData.Channels.Length, SeedData.Messages.Length);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database already seeded — skipping seed data")]
    private partial void DatabaseAlreadySeeded();

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeding ColtonStack workspace...")]
    private partial void SeedingWorkspace();

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {Users} users, {Channels} channels and {Messages} messages")]
    private partial void SeededWorkspace(int users, int channels, int messages);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied extension schema: {Name}")]
    private partial void ExtensionSchemaApplied(string name);
}
