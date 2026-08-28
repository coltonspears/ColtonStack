namespace ColtonStack.Contracts;

/// <summary>Request body for updating the current user's profile.</summary>
public sealed record UpdateProfileRequest(string DisplayName, string AvatarColor);
