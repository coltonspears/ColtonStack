using System.Windows.Threading;
using ColtonStack.Client.Messages;
using ColtonStack.Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// Connectivity chip + retry counter + the chaos switch. Subscribes to resilience pipeline
/// events (retries, send failures) through the messenger — the pipeline in App.xaml.cs has no
/// direct reference to this class, and this class has none to the pipeline.
/// </summary>
public sealed partial class StatusBarViewModel(
    ColtonStackApiClient api,
    Dispatcher dispatcher,
    ILogger<StatusBarViewModel> logger) : ObservableObject, IRecipient<ConnectionStatusMessage>, IRecipient<HttpRetryMessage>
{
    [ObservableProperty]
    public partial ConnectionState Status { get; set; } = ConnectionState.Connecting;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Starting…";

    /// <summary>Retries observed this session — the resilience pipeline made visible.</summary>
    public int RetryCount
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(RetryCountText));
            }
        }
    }

    public string RetryCountText => RetryCount == 0 ? "no retries" : $"{RetryCount} retr{(RetryCount == 1 ? "y" : "ies")}";

    [ObservableProperty]
    public partial bool ChaosEnabled { get; set; }

    partial void OnChaosEnabledChanged(bool value) => _ = ToggleChaosOnServerAsync(value);

    // Deliberately not a [RelayCommand]: the toggle's state lives in the checkbox itself.
    private async Task ToggleChaosOnServerAsync(bool enabled)
    {
        try
        {
            await api.SetChaosAsync(enabled, CancellationToken.None).ConfigureAwait(false);
            ChaosToggled(enabled);
        }
        catch (Exception ex)
        {
            ChaosToggleFailed(ex);
            StatusText = "Chaos toggle failed";
        }
    }

    public void Receive(ConnectionStatusMessage message) =>
        dispatcher.InvokeAsync(() =>
        {
            Status = message.State;
            StatusText = message.Detail;
        });

    public void Receive(HttpRetryMessage message) =>
        dispatcher.InvokeAsync(() =>
        {
            RetryCount++;
            StatusText = message.Detail.Length == 0 ? $"Retrying (attempt {message.Attempt})…" : message.Detail;
        });

    [LoggerMessage(Level = LogLevel.Information, Message = "Chaos mode toggled to {Enabled}")]
    private partial void ChaosToggled(bool enabled);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to toggle chaos mode on the server")]
    private partial void ChaosToggleFailed(Exception exception);
}
