using System.Collections.ObjectModel;
using ColtonStack.Client.Extensions.Commands;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// The composer's <c>/command</c> helper, composed into <see cref="ChatViewModel"/>. Watches
/// the draft, decides whether a slash command is being typed, and produces suggestions: command
/// names while the name is incomplete, then the command's own argument autocomplete (debounced,
/// cancellable). The command registry is the only dependency — extensions supply the commands.
/// </summary>
public sealed partial class SlashCommandInput(ICommandRegistry registry, TimeSpan suggestionDebounce) : ObservableObject, IDisposable
{
    private CancellationTokenSource? _pending;

    public ObservableCollection<SlashSuggestion> Suggestions { get; } = [];

    [ObservableProperty]
    public partial SlashSuggestion? Selected { get; set; }

    /// <summary>True while the draft starts with "/" — the popup shows and Enter/Tab/arrows are redirected here.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    /// <summary>The fully-typed command, once the name is complete (followed by a space).</summary>
    [ObservableProperty]
    public partial CommandDefinition? Command { get; set; }

    /// <summary>Whatever follows the command name, trimmed.</summary>
    [ObservableProperty]
    public partial string Argument { get; set; } = string.Empty;

    /// <summary>Placeholder shown in the popup while the argument is empty.</summary>
    public string Hint => Command?.ArgumentHint ?? string.Empty;

    /// <summary>True when the popup has rows to show — what the view binds the popup's IsOpen to.</summary>
    public bool HasSuggestions => IsActive && Suggestions.Count > 0;

    /// <summary>Status line for the popup: the resolved command's hint, or a nudge when the name is unknown.</summary>
    public string StatusText => Command is { } command
        ? (Argument.Length == 0 && command.ArgumentHint is { } hint ? $"/{command.SlashName} {hint}" : $"/{command.SlashName}  —  {command.Description}")
        : string.Empty;

    partial void OnCommandChanged(CommandDefinition? value)
    {
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnArgumentChanged(string value) => OnPropertyChanged(nameof(StatusText));

    partial void OnIsActiveChanged(bool value) => OnPropertyChanged(nameof(HasSuggestions));

    /// <summary>Re-evaluates the draft. Called from the chat view model whenever <c>Draft</c> changes.</summary>
    public async Task UpdateAsync(string draft)
    {
        CancelPending();

        if (!TryParse(draft, out var name, out var argument, out var nameComplete))
        {
            Reset();
            return;
        }

        IsActive = true;
        Argument = argument;

        if (!nameComplete)
        {
            Command = null;
            ReplaceSuggestions(registry.Commands
                .Where(command => command.SlashName is { } slash && slash.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                .Select(command => new SlashSuggestion(
                    $"/{command.SlashName}",
                    command.Description,
                    CompletedDraft: $"/{command.SlashName} ",
                    IsArgument: false)));
            return;
        }

        var resolved = registry.FindSlash(name);
        Command = resolved;
        if (resolved is null || !resolved.HasSuggestions)
        {
            ReplaceSuggestions([]);
            return;
        }

        _pending = new CancellationTokenSource();
        var token = _pending.Token;
        try
        {
            if (suggestionDebounce > TimeSpan.Zero)
            {
                await Task.Delay(suggestionDebounce, token).ConfigureAwait(true);
            }

            var suggestions = await resolved.SuggestAsync(argument, token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            ReplaceSuggestions(suggestions.Select(suggestion => new SlashSuggestion(
                suggestion.Label,
                suggestion.Detail,
                CompletedDraft: $"/{resolved.SlashName} {suggestion.Value}",
                IsArgument: true)));
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer keystroke
        }
    }

    /// <summary>
    /// Resolves the command to run for the current draft, if the name is complete and known.
    /// </summary>
    public bool TryResolve(out CommandDefinition command, out string argument)
    {
        command = Command!;
        argument = Argument;
        return Command is not null;
    }

    public void MoveSelection(int delta)
    {
        if (Suggestions.Count == 0)
        {
            return;
        }

        var index = Selected is null ? -1 : Suggestions.IndexOf(Selected);
        var next = ((index + delta) % Suggestions.Count + Suggestions.Count) % Suggestions.Count;
        Selected = Suggestions[next];
    }

    public void Reset()
    {
        CancelPending();
        IsActive = false;
        Command = null;
        Argument = string.Empty;
        ReplaceSuggestions([]);
    }

    public void Dispose() => CancelPending();

    private void CancelPending()
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = null;
    }

    private static bool TryParse(string draft, out string name, out string argument, out bool nameComplete)
    {
        name = string.Empty;
        argument = string.Empty;
        nameComplete = false;

        if (draft.Length < 1 || draft[0] != '/' || draft.Contains('\n', StringComparison.Ordinal))
        {
            return false;
        }

        var body = draft[1..];
        var space = body.IndexOf(' ', StringComparison.Ordinal);
        if (space < 0)
        {
            name = body;
            return name.All(static c => char.IsLetterOrDigit(c) || c == '-');
        }

        name = body[..space];
        argument = body[(space + 1)..].Trim();
        nameComplete = true;
        return name.Length > 0;
    }

    private void ReplaceSuggestions(IEnumerable<SlashSuggestion> suggestions)
    {
        Suggestions.Clear();
        foreach (var suggestion in suggestions)
        {
            Suggestions.Add(suggestion);
        }

        Selected = Suggestions.Count > 0 ? Suggestions[0] : null;
        OnPropertyChanged(nameof(HasSuggestions));
    }
}