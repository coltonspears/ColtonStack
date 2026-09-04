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
    IUserService users,
    IAuditService auditService,
    IHubContext<ChatHub, IChatHubClient> hubContext,
    IWebhookOutbox webhookOutbox) : IMessageService
{
    // The two queries in this service that earn hand-written SQL: a paged two-table join, in
    // two flavours. Every existence check and insert below is Dapper.Contrib CRUD derived from
    // the row classes.
    private const string MessageColumnsSql = """
        SELECT m.Id, m.ChannelId, m.UserId,
               u.DisplayName AS AuthorName, u.AvatarColor AS AuthorColor,
               m.Text, m.CreatedAtUtc, m.AttachmentKind, m.AttachmentJson
        """;

    /// <summary>Opening a channel: the newest <c>limit</c> messages, returned oldest-first.</summary>
    private const string LatestPageSql = MessageColumnsSql + """

        FROM (SELECT * FROM Messages
              WHERE ChannelId = @channelId
              ORDER BY Id DESC
              LIMIT @limit) m
        JOIN Users u ON u.Id = m.UserId
        ORDER BY m.Id
        """;

    /// <summary>Reconnect catch-up: everything after the newest id the client already holds, capped.</summary>
    private const string CatchUpPageSql = MessageColumnsSql + """

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

        var rows = await connection.QueryAsync<MessagePageRow>(
            afterId > 0 ? CatchUpPageSql : LatestPageSql,
            new { channelId, afterId, limit }).ConfigureAwait(false);
        return [.. rows.Select(row => row.ToDto())];
    }

    public async Task<MessageDto> SendAsync(long channelId, string text, MessageAttachmentDto? attachment, CancellationToken cancellationToken)
    {
        var self = await users.GetSelfAsync(cancellationToken).ConfigureAwait(false);
        return await SaveAsync(channelId, self, text, attachment, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MessageDto> SendAsUserAsync(long channelId, long userId, string text, CancellationToken cancellationToken)
    {
        var author = await users.FindAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User {userId} does not exist.");
        return await SaveAsync(channelId, author, text, attachment: null, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MessageDto> SaveAsync(long channelId, UserDto author, string text, MessageAttachmentDto? attachment, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        _ = await connection.GetAsync<ChannelRow>(channelId).ConfigureAwait(false)
            ?? throw new ChannelNotFoundException(channelId);

        var row = new MessageRow
        {
            ChannelId = channelId,
            UserId = author.Id,
            Text = text,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            AttachmentKind = attachment?.Kind,
            AttachmentJson = attachment?.PayloadJson,
        };

        // InsertAsync writes the generated key back into row.Id — no follow-up SELECT needed.
        await connection.InsertAsync(row).ConfigureAwait(false);

        var message = new MessageDto(
            row.Id, row.ChannelId, author.Id,
            author.DisplayName, author.AvatarColor,
            row.Text, row.CreatedAtUtc, attachment);

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

    /// <summary>Flat shape of the join above; Dapper fills it, <see cref="ToDto"/> folds the two attachment columns into one record.</summary>
    private sealed class MessagePageRow
    {
        public long Id { get; set; }

        public long ChannelId { get; set; }

        public long UserId { get; set; }

        public string AuthorName { get; set; } = string.Empty;

        public string AuthorColor { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public DateTimeOffset CreatedAtUtc { get; set; }

        public string? AttachmentKind { get; set; }

        public string? AttachmentJson { get; set; }

        public MessageDto ToDto() => new(
            Id, ChannelId, UserId, AuthorName, AuthorColor, Text, CreatedAtUtc,
            AttachmentKind is { Length: > 0 } kind && AttachmentJson is { } json ? new MessageAttachmentDto(kind, json) : null);
    }
}
