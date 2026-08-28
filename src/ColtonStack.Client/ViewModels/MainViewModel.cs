using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// The shell: wires the three regions together and exposes them for binding. Everything else
/// (loading, sending, status, unread badges) happens inside the child view models.
/// </summary>
public sealed partial class MainViewModel(
    ChannelListViewModel channels,
    ChatViewModel chat,
    StatusBarViewModel status,
    ILogger<MainViewModel> logger) : ObservableObject
{
    public ChannelListViewModel Channels { get; } = channels;

    public ChatViewModel Chat { get; } = chat;

    public StatusBarViewModel Status { get; } = status;

    /// <summary>Kick off the initial load after the window is visible.</summary>
    public async Task InitializeAsync()
    {
        await Channels.LoadCommand.ExecuteAsync(null).ConfigureAwait(true);
        StartupComplete();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "ColtonStack client initialized")]
    private partial void StartupComplete();
}
