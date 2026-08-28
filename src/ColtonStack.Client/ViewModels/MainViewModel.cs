using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// The shell: wires the regions together and exposes them for binding. Everything else
/// (loading, sending, status, unread badges, profile editing) happens inside the child
/// view models — composition, not a god class.
/// </summary>
public sealed partial class MainViewModel(
    ChannelListViewModel channels,
    ChatViewModel chat,
    StatusBarViewModel status,
    PeopleViewModel people,
    SettingsViewModel settings,
    ILogger<MainViewModel> logger) : ObservableObject
{
    public ChannelListViewModel Channels { get; } = channels;

    public ChatViewModel Chat { get; } = chat;

    public StatusBarViewModel Status { get; } = status;

    public PeopleViewModel People { get; } = people;

    public SettingsViewModel Settings { get; } = settings;

    /// <summary>Which sidebar panel is visible; the rail's Home/People tiles select it.</summary>
    [ObservableProperty]
    public partial SidebarPane ActivePane { get; set; } = SidebarPane.Channels;

    partial void OnActivePaneChanged(SidebarPane value)
    {
        // Lazy-load the directory the first time it's opened; the refresh button re-runs it.
        if (value == SidebarPane.People && People.People.Count == 0)
        {
            _ = People.LoadCommand.ExecuteAsync(null);
        }
    }

    /// <summary>Kick off the initial load after the window is visible.</summary>
    public async Task InitializeAsync()
    {
        await Channels.LoadCommand.ExecuteAsync(null).ConfigureAwait(true);
        StartupComplete();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "ColtonStack client initialized")]
    private partial void StartupComplete();
}
