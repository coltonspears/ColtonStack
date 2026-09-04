namespace ColtonStack.Contracts;

/// <summary>One persisted preference. Keys are dotted, lower-case, owned by the extension that defines them.</summary>
public sealed record SettingDto(string Key, string Value, DateTimeOffset UpdatedAtUtc);
