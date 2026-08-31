using ColtonStack.Client.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// The shell: wires the regions together and exposes them for binding. Everything else
/// (loading, sending, status, unread badges, profile editing) happens inside the child
/// view models — composition, not a god class.
///
/// Sidebar panes come from <see cref="SidebarPaneRegistry"/>: navigation nodes registered by
/// extensions, ordered explicitly, selected by id — there is no pane enum in the core app,
/// so an extension can add a pane without the shell ever being edited.
/// </summary>
public sealed partial class MainViewModel(
    ChatViewModel chat,
    StatusBarViewModel status,
    SettingsViewModel settings,
    DiagnosticsViewModel diagnostics,
    SidebarPaneRegistry paneRegistry,
    ILogger<MainViewModel> logger) : ObservableObject
{
    public ChatViewModel Chat { get; } = chat;

    public StatusBarViewModel Status { get; } = status;

    public SettingsViewModel Settings { get; } = settings;

    public DiagnosticsViewModel Diagnostics { get; } = diagnostics;

    /// <summary>All registered panes, ordered by their explicit <c>Order</c>. The rail binds to this.</summary>
    public IReadOnlyList<SidebarPaneDefinition> Panes { get; } = paneRegistry.Panes;

    /// <summary>The active pane. Extensions add candidates; the shell only ever holds one.</summary>
    [ObservableProperty]
    public partial SidebarPaneDefinition? ActivePane { get; set; }

    /// <summary>The active pane's content (its view model) — an implicit DataTemplate renders it.</summary>
    public object? ActiveContent => ActivePane?.Content;

    partial void OnActivePaneChanged(SidebarPaneDefinition? value)
    {
        OnPropertyChanged(nameof(ActiveContent));

        // The pane's own activation hook runs here — core panes lazy-load their lists,
        // the audit pane refreshes, and extensions do whatever they registered.
        if (value is not null)
        {
            _ = value.ActivateAsync();
        }
    }

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