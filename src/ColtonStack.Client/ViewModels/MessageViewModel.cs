using System.Globalization;
using ColtonStack.Contracts;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// Immutable presentation wrapper around a <see cref="MessageDto"/> — it carries zero mutable
/// state, so it needs no INotifyPropertyChanged at all. Not every model needs to be observable;
/// only things that change need to notify. (Contrast: legacy VMs inherited observability,
/// auditing, persistence, and more from one heavy base class.)
/// </summary>
public sealed class MessageViewModel(MessageDto message, bool isFirstOfGroup, bool isFirstOfDay = false, object? attachment = null)
{
    public long Id { get; } = message.Id;

    public string AuthorName { get; } = message.AuthorName;

    public string AvatarColor { get; } = message.AuthorColor;

    public string Text { get; } = message.Text;

    public DateTimeOffset CreatedAtUtc { get; } = message.CreatedAtUtc;

    /// <summary>Consecutive messages by the same author collapse into one visual group.</summary>
    public bool IsFirstOfGroup { get; } = isFirstOfGroup;

    /// <summary>First message of a calendar day (local time) — the list shows a date divider above it.</summary>
    public bool IsFirstOfDay { get; } = isFirstOfDay;

    /// <summary>Extension-rendered rich content (a Pokémon card, ...) or null for a plain text message.</summary>
    public object? Attachment { get; } = attachment;

    public bool HasAttachment => Attachment is not null;

    public string Initials { get; } = NameInitials.From(message.AuthorName);

    public string TimeText { get; } = message.CreatedAtUtc.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);

    public string DateHeader { get; } = DateHeaderFor(message.CreatedAtUtc.ToLocalTime(), DateTimeOffset.Now);

    /// <summary>"Today", "Yesterday", or a full date — Slack's divider wording.</summary>
    public static string DateHeaderFor(DateTimeOffset local, DateTimeOffset now)
    {
        var day = DateOnly.FromDateTime(local.DateTime);
        var today = DateOnly.FromDateTime(now.LocalDateTime);
        if (day == today)
        {
            return "Today";
        }

        if (day == today.AddDays(-1))
        {
            return "Yesterday";
        }

        return day.Year == today.Year
            ? local.ToString("dddd, MMMM d", CultureInfo.CurrentCulture)
            : local.ToString("D", CultureInfo.CurrentCulture);
    }
}
