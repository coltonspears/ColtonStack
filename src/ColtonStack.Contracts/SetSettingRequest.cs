namespace ColtonStack.Contracts;

/// <summary>Body of <c>PUT /api/settings/{key}</c>.</summary>
public sealed record SetSettingRequest(string Value);
