namespace ColtonStack.Client.Messages;

/// <summary>Opens the in-window Settings view, optionally on a specific section. Null id means "whatever was open last".</summary>
public sealed record SettingsRequestedMessage(string? SectionId = null);
