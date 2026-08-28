using System.Globalization;
using ColtonStack.Contracts;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// Immutable presentation wrapper around a <see cref="MessageDto"/> — it carries zero mutable
/// state, so it needs no INotifyPropertyChanged at all. Not every model needs to be observable;
/// only things that change need to notify. (Contrast: legacy VMs inherited observability,
/// auditing, persistence, and more from one heavy base class.)
/// </summary>
public sealed class MessageViewModel(MessageDto message, bool isFirstOfGroup)
{
    public long Id { get; } = message.Id;

    public string AuthorName { get; } = message.AuthorName;

    public string AvatarColor { get; } = message.AuthorColor;

    public string Text { get; } = message.Text;

    public DateTimeOffset CreatedAtUtc { get; } = message.CreatedAtUtc;

    /// <summary>Consecutive messages by the same author collapse into one visual group.</summary>
    public bool IsFirstOfGroup { get; } = isFirstOfGroup;

    public string Initials { get; } = string.Concat(
        message.AuthorName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(part => part[0]))
        .ToUpperInvariant();

    public string TimeText { get; } = message.CreatedAtUtc.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
}
