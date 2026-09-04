using ColtonStack.Client.Extensions;
using ColtonStack.Client.Messages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// The shell: wires the regions together and exposes them for binding. Everything else
/// (loading, sending, status, unread badges, settings) happens inside the child view models —
/// composition, not a god class.
///
/// Sidebar panes come from <see cref="ISidebarPaneRegistry"/>: navigation nodes registered by
/// extensions, ordered explicitly, selected by id — there is no pane enum in the core app.
/// Navigation requests from elsewhere (the command palette) arrive as messages, so nothing
/// needs a reference to the shell to move it.
/// </summary>
public sealed partial class MainViewModel(
    ChatViewModel chat,
    StatusBarViewModel status,
    SettingsViewModel settings,
    DiagnosticsViewModel diagnostics,
    CommandPaletteViewModel palette,
    ISidebarPaneRegistry paneRegistry,
    ILogger<MainViewModel> logger) : ObservableObject, IRecipient<PaneRequestedMessage>, IRecipient<SettingsRequestedMessage>
{
    public ChatViewModel Chat { get; } = chat;

    public StatusBarViewModel Status { get; } = status;

    public SettingsViewModel Settings { get; } = settings;

    public DiagnosticsViewModel Diagnostics { get; } = diagnostics;

    public CommandPaletteViewModel Palette { get; } = palette;

    /// <summary>All registered panes, ordered by their explicit <c>Order</c>. The rail binds to this.</summary>
    public IReadOnlyList<SidebarPaneDefinition> Panes { get; } = paneRegistry.Panes;

    /// <summary>The active pane. Extensions add candidates; the shell only ever holds one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveContent))]
    public partial SidebarPaneDefinition? ActivePane { get; set; }

    /// <summary>When true the main region shows the Settings page instead of the conversation.</summary>
    [ObservableProperty]
    public partial bool IsSettingsOpen { get; set; }

    /// <summary>The active pane's content (its view model) — an implicit DataTemplate renders it.</summary>
    public object? ActiveContent => ActivePane?.Content;

    partial void OnActivePaneChanged(SidebarPaneDefinition? value)
    {
        // The pane's own activation hook runs here — core panes lazy-load their lists,
        // the audit pane refreshes, and extensions do whatever they registered.
        if (value is not null)
        {
            _ = value.ActivateAsync();
        }
    }

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

    [RelayCommand]
    private void CloseSettings() => IsSettingsOpen = false;

    /// <summary>Escape: close the topmost transient surface — palette first, then settings, then the log panel.</summary>
    [RelayCommand]
    private void Dismiss()
    {
        if (Palette.IsOpen)
        {
            Palette.IsOpen = false;
        }
        else if (IsSettingsOpen)
        {
            IsSettingsOpen = false;
        }
        else if (Diagnostics.IsOpen)
        {
            Diagnostics.IsOpen = false;
        }
    }

    public void Receive(PaneRequestedMessage message)
    {
        var pane = Panes.FirstOrDefault(candidate => string.Equals(candidate.Id, message.PaneId, StringComparison.OrdinalIgnoreCase));
        if (pane is not null)
        {
            ActivePane = pane;
            IsSettingsOpen = false;
        }
    }

    public void Receive(SettingsRequestedMessage message) => IsSettingsOpen = true;

    /// <summary>Selects the first registered pane; its activation hook kicks off the initial load.</summary>
    public Task InitializeAsync()
    {
        // Index access (not FirstOrDefault) — the registry is a plain list and CA1826 agrees.
        ActivePane = Panes.Count > 0 ? Panes[0] : null;
        StartupComplete(Panes.Count);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "ColtonStack client initialized with {PaneCount} registered pane(s)")]
    private partial void StartupComplete(int paneCount);
}
