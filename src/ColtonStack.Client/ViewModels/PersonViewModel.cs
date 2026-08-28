using ColtonStack.Contracts;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// Immutable presentation wrapper around a <see cref="UserDto"/> — like <see cref="MessageViewModel"/>,
/// it never changes, so it needs no INotifyPropertyChanged. Profile updates replace the row.
/// </summary>
public sealed class PersonViewModel(UserDto user)
{
    public long Id { get; } = user.Id;

    public string DisplayName { get; } = user.DisplayName;

    public string AvatarColor { get; } = user.AvatarColor;

    public bool IsSelf { get; } = user.IsSelf;

    public string Initials { get; } = NameInitials.From(user.DisplayName);
}
