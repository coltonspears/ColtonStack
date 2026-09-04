namespace ColtonStack.Client.ViewModels;

/// <summary>A row in the slash popup. Accepting it replaces the composer text with <see cref="CompletedDraft"/>; argument rows can be sent straight away.</summary>
public sealed record SlashSuggestion(string Label, string Detail, string CompletedDraft, bool IsArgument);
