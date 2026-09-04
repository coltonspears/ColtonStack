using System.Collections.ObjectModel;
using ColtonStack.Client.Extensions.Commands;
using ColtonStack.Client.Messages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// The title-bar command palette (Ctrl+K). It knows two things: the command registry and the
/// messenger. Every row it shows was contributed by an extension — static commands or dynamic
/// sources such as "jump to #channel" — and every row executes a delegate the extension wrote.
/// The palette never references another view model, so adding a command never touches it.
/// </summary>
public sealed partial class CommandPaletteViewModel(
    ICommandRegistry registry,
    IMessenger messenger,
    ILogger<CommandPaletteViewModel> logger) : ObservableObject, IDisposable
{
    private static readonly TimeSpan QueryDebounce = TimeSpan.FromMilliseconds(120);
    private const int MaxResults = 12;

    private CancellationTokenSource? _refresh;

    public ObservableCollection<CommandItem> Results { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunSelectedCommand))]
    public partial CommandItem? Selected { get; set; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    public bool HasResults => Results.Count > 0;

    partial void OnIsOpenChanged(bool value)
    {
        if (value)
        {
            Query = string.Empty;
            _ = RefreshAsync(immediate: true);
        }
        else
        {
            _refresh?.Cancel();
        }
    }

    partial void OnQueryChanged(string value)
    {
        if (IsOpen)
        {
            _ = RefreshAsync(immediate: false);
        }
    }

    [RelayCommand]
    private void Open() => IsOpen = true;

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand]
    private void Toggle() => IsOpen = !IsOpen;

    [RelayCommand]
    private void MoveDown() => MoveSelection(+1);

    [RelayCommand]
    private void MoveUp() => MoveSelection(-1);

    private bool CanRunSelected() => Selected is not null;

    /// <summary>Enter in the query box.</summary>
    [RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(CanRunSelected))]
    private Task RunSelectedAsync(CancellationToken cancellationToken) =>
        Selected is { } item ? RunItemAsync(item, cancellationToken) : Task.CompletedTask;

    /// <summary>Mouse click on a row.</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RunItemAsync(CommandItem? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        IsOpen = false;
        try
        {
            await item.ExecuteAsync(cancellationToken);
            CommandRan(item.Title);
        }
        catch (Exception ex)
        {
            CommandFailed(ex, item.Title);
            messenger.Send(new HttpRetryMessage(0, $"{item.Title} failed: {ex.Message}"));
        }
    }

    /// <summary>Recomputes the result list for the current query; each keystroke cancels the previous computation.</summary>
    public async Task RefreshAsync(bool immediate)
    {
        var token = RestartRefresh();

        try
        {
            if (!immediate)
            {
                await Task.Delay(QueryDebounce, token);
            }

            IsSearching = true;
            var query = Query.Trim();
            var items = new List<CommandItem>(MatchCommands(query));

            foreach (var source in registry.Sources)
            {
                items.AddRange(await source.GetItemsAsync(query, token));
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            Results.Clear();
            foreach (var item in items.Take(MaxResults))
            {
                Results.Add(item);
            }

            Selected = Results.Count > 0 ? Results[0] : null;
            OnPropertyChanged(nameof(HasResults));
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer keystroke or the palette closed
        }
        catch (Exception ex)
        {
            SourceFailed(ex);
        }
        finally
        {
            IsSearching = false;
        }
    }

    private IEnumerable<CommandItem> MatchCommands(string query) =>
        registry.Commands
            .Where(command => Matches(command, query))
            .Select(command => new CommandItem(
                command.Title,
                command.SlashName is { } slash ? $"{command.Description}  ·  /{slash}" : command.Description,
                command.IconGlyph,
                command.Category,
                cancellationToken => command.ExecuteAsync(new CommandInvocation(string.Empty, ChannelId: null, cancellationToken))));

    /// <summary>Every whitespace-separated token must appear in the title, category or a keyword.</summary>
    public static bool Matches(CommandDefinition command, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }

        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.All(token =>
            command.Title.Contains(token, StringComparison.OrdinalIgnoreCase)
            || command.Category.Contains(token, StringComparison.OrdinalIgnoreCase)
            || command.Keywords.Any(keyword => keyword.Contains(token, StringComparison.OrdinalIgnoreCase))
            || (command.SlashName is { } slash && slash.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }

    public void Dispose()
    {
        _refresh?.Cancel();
        _refresh?.Dispose();
    }

    /// <summary>Cancels any in-flight refresh and hands back the token for the next one.</summary>
    private CancellationToken RestartRefresh()
    {
        _refresh?.Cancel();
        _refresh?.Dispose();
        _refresh = new CancellationTokenSource();
        return _refresh.Token;
    }

    private void MoveSelection(int delta)
    {
        if (Results.Count == 0)
        {
            return;
        }

        var index = Selected is null ? -1 : Results.IndexOf(Selected);
        var next = ((index + delta) % Results.Count + Results.Count) % Results.Count;
        Selected = Results[next];
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Palette command '{Title}' ran")]
    private partial void CommandRan(string title);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Palette command '{Title}' failed")]
    private partial void CommandFailed(Exception exception, string title);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A palette item source failed")]
    private partial void SourceFailed(Exception exception);
}
