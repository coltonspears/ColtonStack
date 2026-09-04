namespace ColtonStack.Contracts;

/// <summary>
/// A chat message as it travels over the wire and out of Dapper.
/// Positional record: the database row maps straight into the constructor —
/// no mutable state, no reflection-based hydration, no "magic" base class.
/// <see cref="Attachment"/> is optional structured content owned by an extension.
/// </summary>
public sealed record MessageDto(
    long Id,
    long ChannelId,
    long UserId,
    string AuthorName,
    string AuthorColor,
    string Text,
    DateTimeOffset CreatedAtUtc,
    MessageAttachmentDto? Attachment = null);
