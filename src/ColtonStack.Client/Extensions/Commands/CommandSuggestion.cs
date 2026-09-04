namespace ColtonStack.Client.Extensions.Commands;

/// <summary>One autocomplete candidate for a slash command's argument. <see cref="Value"/> is what lands in the composer.</summary>
public sealed record CommandSuggestion(string Label, string Value, string Detail = "");
