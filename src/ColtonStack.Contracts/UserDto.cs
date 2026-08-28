namespace ColtonStack.Contracts;

/// <summary>A chat user. <see cref="IsSelf"/> marks the identity this client posts as.</summary>
public sealed record UserDto(
    long Id,
    string DisplayName,
    string AvatarColor,
    bool IsSelf);
