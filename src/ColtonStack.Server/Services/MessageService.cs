using ColtonStack.Contracts;
using ColtonStack.Server.Data;
using ColtonStack.Server.Hubs;
using ColtonStack.Server.Infrastructure;
using ColtonStack.Server.Webhooks;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.SignalR;

namespace ColtonStack.Server.Services;

public sealed class MessageService(
    IDbConnectionFactory connectionFactory,
    IAuditService auditService,
    IHubContext<ChatHub, IChatHubClient> hubContext,
    IWebhookOutbox webhookOutbox) : IMessageService
{
    // The one query in this service that earns hand-written SQL: a paged two-table join.
    // Every existence check and insert below is Dapper.Contrib CRUD derived from the row classes.
    private const string MessagePageSql = """
        SELECT m.Id, m.ChannelId, m.UserId,
               u.DisplayName AS AuthorName, u.AvatarColor AS AuthorColor,
               m.Text, m.CreatedAtUtc
        FROM Messages m
        JOIN Users u ON u.Id = m.UserId
        WHERE m.ChannelId = @channelId AND m.Id > @afterId
        ORDER BY m.Id
        LIMIT @limit
        """;

    public async Task<IReadOnlyList<MessageDto>> GetRecentAsync(
        long channelId,
        long afterId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        _ = await connection.GetAsync<ChannelRow>(channelId).ConfigureAwait(false)
            ?? throw new ChannelNotFoundException(channelId);

        var messages = await connection.QueryAsync<MessageDto>(
            MessagePageSql,
            new { channelId, afterId, limit }).ConfigureAwait(false);
        return [.. messages];
    }

    public Task<MessageDto> SendAsync(long channelId, SendMessageRequest request, CancellationToken cancellationToken) =>
        SaveAsync(channelId, authorUserId: null, request.Text, cancellationToken);

    public Task<MessageDto> SendAsUserAsync(long channelId, long userId, string text, CancellationToken cancellationToken) =>
        SaveAsync(channelId, authorUserId: userId, text, cancellationToken);

    private async Task<MessageDto> SaveAsync(long channelId, long? authorUserId, string text, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        _ = await connection.GetAsync<ChannelRow>(channelId).ConfigureAwait(false)
            ?? throw new ChannelNotFoundException(channelId);

        // Messages from the client have no author id — they're always "us". The user table is a
        // handful of demo rows, so scanning it in memory beats another hand-written WHERE clause.
        var author = authorUserId is { } userId
            ? await connection.GetAsync<UserRow>(userId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"User {userId} does not exist.")
            : (await connection.GetAllAsync<UserRow>().ConfigureAwait(false)).First(user => user.IsSelf);

        var row = new MessageRow
        {
            ChannelId = channelId,
            UserId = author.Id,
            Text = text,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        // InsertAsync writes the generated key back into row.Id — no follow-up SELECT needed.
        await connection.InsertAsync(row).ConfigureAwait(false);

        var message = new MessageDto(
            row.Id, row.ChannelId, author.Id,
            author.DisplayName, author.AvatarColor,
            row.Text, row.CreatedAtUtc);

        // Composed pipeline — each step is an injected service the record knows nothing about.
        await auditService.RecordAsync(
            entityType: "message",
            entityId: message.Id,
            action: "created",
            actor: message.AuthorName,
            entity: message,
            entityJsonTypeInfo: ColtonStackJsonContext.Default.MessageDto,
            cancellationToken).ConfigureAwait(false);

        await hubContext.Clients
            .Group(ChatHub.GroupNameFor(channelId))
            .MessagePostedAsync(message)
            .ConfigureAwait(false);

        await webhookOutbox.EnqueueAsync(
            new WebhookJob("message.posted", message),
            cancellationToken).ConfigureAwait(false);

        return message;
    }
}
