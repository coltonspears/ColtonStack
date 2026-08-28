using ColtonStack.Contracts;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Server.Infrastructure;

/// <summary>
/// Creates the SQLite schema on startup and seeds a believable workspace the first time the
/// server runs. Runs as a hosted service — the Generic Host manages its lifetime.
/// </summary>
public sealed partial class SqliteDatabaseInitializer(
    IDbConnectionFactory connectionFactory,
    ILogger<SqliteDatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        DapperConfig.Register();

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await CreateSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await SeedIfEmptyAsync(connection).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task CreateSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var schema = """
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
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                ChannelId    INTEGER NOT NULL REFERENCES Channels(Id),
                UserId       INTEGER NOT NULL REFERENCES Users(Id),
                Text         TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL);

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
            """;

        var command = connection.CreateCommand();
        command.CommandText = schema;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedIfEmptyAsync(SqliteConnection connection)
    {
        var userCount = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Users").ConfigureAwait(false);
        if (userCount > 0)
        {
            DatabaseAlreadySeeded();
            return;
        }

        SeedingWorkspace();
        await using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);

        await connection.ExecuteAsync(
            "INSERT INTO Users (Id, DisplayName, AvatarColor, IsSelf) VALUES (@Id, @Name, @Color, @IsSelf)",
            SeedData.Users.Select(u => new { u.Id, u.Name, u.Color, u.IsSelf }),
            transaction).ConfigureAwait(false);

        await connection.ExecuteAsync(
            "INSERT INTO Channels (Id, Name, Topic) VALUES (@Id, @Name, @Topic)",
            SeedData.Channels.Select(c => new { c.Id, c.Name, c.Topic }),
            transaction).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        await connection.ExecuteAsync(
            """
            INSERT INTO Messages (ChannelId, UserId, Text, CreatedAtUtc)
            VALUES (@Channel, (SELECT Id FROM Users WHERE DisplayName = @Author), @Text, @CreatedAtUtc)
            """,
            SeedData.Messages.Select(m => new { m.Channel, m.Author, m.Text, CreatedAtUtc = now.AddMinutes(-m.MinutesAgo) }),
            transaction).ConfigureAwait(false);

        await transaction.CommitAsync().ConfigureAwait(false);
        SeededWorkspace(SeedData.Users.Length, SeedData.Channels.Length, SeedData.Messages.Length);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database already seeded — skipping seed data")]
    private partial void DatabaseAlreadySeeded();

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeding ColtonStack workspace...")]
    private partial void SeedingWorkspace();

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {Users} users, {Channels} channels and {Messages} messages")]
    private partial void SeededWorkspace(int users, int channels, int messages);
}
