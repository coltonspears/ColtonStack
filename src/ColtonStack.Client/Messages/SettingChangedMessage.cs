namespace ColtonStack.Client.Messages;

/// <summary>Published by the settings store after a value is saved, so anything showing that setting re-reads it.</summary>
public sealed record SettingChangedMessage(string Key, string Value);
